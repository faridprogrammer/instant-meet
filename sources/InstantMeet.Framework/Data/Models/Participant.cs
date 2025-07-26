using System.ComponentModel.DataAnnotations;

namespace InstantMeet.Framework.Data.Model;

public class Participant
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ConnectionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string UserName { get; set; }

    public int RoomId { get; set; }
    public Room Room { get; set; }

    public bool IsCameraOn { get; set; } = true;
    public bool IsMicOn { get; set; } = true;
}