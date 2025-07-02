using System;
using System.ComponentModel.DataAnnotations;

public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string User { get; set; }

    [Required]
    public string Message { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
