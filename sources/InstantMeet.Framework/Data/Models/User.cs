using System;
using InstantMeet.Framework.Data.Model;

namespace InstantMeet.Framework.Data.Models;

public class User
{
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Password { get; set; }
    public ICollection<Room> Rooms { get; set; }
}
