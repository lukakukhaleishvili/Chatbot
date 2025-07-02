ჩეთი აპლიკაცია ASP.NET Core 8.0 + SignalR + Identity-თი
მიმოხილვა
ეს არის რეალურ დროში ჩეთი აპლიკაცია ASP.NET Core 8.0 MVC-ში, სადაც:

მომხმარებლების რეგისტრაცია და ავტორიზაცია ხდება ASP.NET Core Identity-თი

რეალურ დროში მესიჯების გაგზავნა და მიღება ხდება SignalR-ის საშუალებით

არის ონლაინ/ოფლაინ მომხმარებლების სია

ყველა შეტყობინება ინახება ბაზაში Entity Framework Core-ით (MS SQL LocalDB)

Logout ფუნქციონალი

მარტივი და გამჭვირვალე UI

ტექნოლოგიები და გარემო
.NET 8.0 ASP.NET Core MVC

SignalR რეალურ დროში კომუნიკაციისთვის

Entity Framework Core (Code First)

ASP.NET Core Identity მომხმარებელთა მართვისთვის

MS SQL LocalDB

Git ვერსიის კონტროლისთვის

HTML/CSS/JavaScript UI-სთვის

ინსტრუქცია
1. პროექტის კონფიგურაცია (Program.cs)
csharp
Copy
Edit
var builder = WebApplication.CreateBuilder(args);

// მომსახურების რეგისტრაცია
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();  // Identity-ს Razor Pages-ის მხარდაჭერისთვის
builder.Services.AddSignalR();

// DB და Identity კონფიგურაცია
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<IdentityUser>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();  // აუცილებელია, რომ Identity მუშაობდეს
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();  // Identity-ს გვერდების მეპინგი
app.MapHub<ChatHub>("/chathub");  // SignalR ჰაბის მეპინგი

app.Run();
2. ბაზის და მოდელების დაყენება
შექმენი ApplicationDbContext რომელსაც მემკვიდრეობით ეკუთვნის IdentityDbContext

დაამატე ChatMessage Entity მოდელი შეტყობინებებისთვის:

csharp
Copy
Edit
public class ChatMessage
{
    public int Id { get; set; }
    public string User { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
DbContext-ში ჩაამატე:

csharp
Copy
Edit
public DbSet<ChatMessage> ChatMessages { get; set; }
დააწარმოე მიგრაციები:

bash
Copy
Edit
dotnet ef migrations add InitialCreate
dotnet ef database update
3. Identity მომხმარებლების რეგისტრაცია და ავტორიზაცია
ASP.NET Core Identity ავტომატურად უზრუნველყოფს რეგისტრაციას, შესვლას და გამოსვლას Razor Pages-ით

Logout მეთოდი AccountController-ში ან Razor Page-ში:

csharp
Copy
Edit
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Logout()
{
    await _signInManager.SignOutAsync();
    return RedirectToAction("Index", "Home");
}
4. SignalR ChatHub
csharp
Copy
Edit
public class ChatHub : Hub
{
    private static readonly HashSet<string> ConnectedUsers = new();
    private readonly ApplicationDbContext _context;

    public ChatHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var userName = Context.User?.Identity?.Name ?? Context.ConnectionId;
        ConnectedUsers.Add(userName);
        await Clients.All.SendAsync("UserConnected", userName, ConnectedUsers.ToList());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userName = Context.User?.Identity?.Name ?? Context.ConnectionId;
        ConnectedUsers.Remove(userName);
        await Clients.All.SendAsync("UserDisconnected", userName, ConnectedUsers.ToList());
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string user, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        await Clients.All.SendAsync("ReceiveMessage", user, message, timestamp);

        // შეინახე შეტყობინება ბაზაში
        var chatMessage = new ChatMessage { User = user, Message = message, Timestamp = DateTime.Now };
        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();
    }
}
5. შენ მიერ მოცემული HTML და JavaScript (Chat UI)
ჩაწერე Views/Home/Index.cshtml-ში (თუ შენ იყენებ MVC-ის View):

razor
Copy
Edit
@{
    ViewData["Title"] = "Chat";
}

<!-- Google Fonts და CSS სტილები შენ მიერ მოცემულია სრულად -->

<div class="chat-container">
    <div class="chat-header">
        <h2>Chat Room</h2>
        <form asp-controller="Account" asp-action="Logout" method="post">
            <button id="logoutButton" type="submit">Logout</button>
        </form>
    </div>

    <div class="username-box">
        <input type="text" id="usernameInput" placeholder="შეიყვანე შენი სახელი..." />
        <button id="setUsernameBtn">სახელის განთავსება</button>
    </div>

    <ul id="userList" class="user-list"></ul>

    <div id="messagesList"></div>

    <div class="input-group">
        <input type="text" id="messageInput" placeholder="შეიყვანე შეტყობინება..." />
        <button id="sendButton">გაგზავნა</button>
    </div>
</div>

@section Scripts {
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js"></script>
    <script>
        let currentUserName = localStorage.getItem("chatUsername") || "";

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/chathub")
            .build();

        connection.on("ReceiveMessage", (user, message, time) => {
            const msg = document.createElement("div");
            msg.classList.add("message");
            msg.innerHTML = `<strong>${user || "Unknown"} [${time}]:</strong> ${message}`;
            document.getElementById("messagesList").appendChild(msg);
            document.getElementById("messagesList").scrollTop = messagesList.scrollHeight;
        });

        connection.on("UserConnected", (user, users) => updateUsers(users));
        connection.on("UserDisconnected", (user, users) => updateUsers(users));

        connection.start().catch(err => console.error(err.toString()));

        document.getElementById("sendButton").addEventListener("click", () => {
            const message = document.getElementById("messageInput").value;
            if (message.trim() !== "") {
                connection.invoke("SendMessage", currentUserName || "Anonymous", message)
                    .catch(err => console.error(err.toString()));
                document.getElementById("messageInput").value = "";
            }
        });

        function updateUsers(users) {
            const ul = document.getElementById("userList");
            ul.innerHTML = "";
            users.forEach(u => {
                const li = document.createElement("li");
                li.innerHTML = `<span class="user-status online"></span>${u || "Anonymous"}`;
                ul.appendChild(li);
            });
        }

        document.getElementById("setUsernameBtn").addEventListener("click", () => {
            const input = document.getElementById("usernameInput").value.trim();
            if (input) {
                currentUserName = input;
                localStorage.setItem("chatUsername", input);
                alert("სახელი დარეგისტრირდა: " + currentUserName);
            }
        });

        window.addEventListener("DOMContentLoaded", () => {
            if (currentUserName) {
                document.getElementById("usernameInput").value = currentUserName;
            }
        });
    </script>
}
6. მოკლე ახსნა:
SignalR: იძახებ შენს ChatHub-ს რეალურ დროში, გირეკავ SendMessage ფუნქციას და იღებ მესიჯებს

Identity: მართავს მომხმარებლების რეგისტრაციას, ავტორიზაციას და ლოგაუთს

EF Core: ინახავს ყველა მესიჯს ბაზაში

UI: სუფთა და მარტივი, მომხმარებლის სახელის შეყვანა და მესიჯების გაგზავნა/მიღება

Online Users: იცვლება მომხმარებელთა სია რეალურ დროში, როგორც მომხმარებლები კავშირობენ/გაშვებიან

7. როგორ აწარმოო პროექტი
დააყენე appsettings.json-ში შენი LocalDB-ის დაკავშირების სტრიქონი

გადაჰყევი მიგრაციებს:

bash
Copy
Edit
dotnet ef database update
შემდეგ ჩართე:

bash
Copy
Edit
dotnet run
გადადი ბრაუზერში https://localhost:5001 (ან შესაბამისი პორტი)

რეგისტრაცია, ჩართვა და ჩეთის გამოყენება

