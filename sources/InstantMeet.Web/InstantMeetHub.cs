using System;
using InstantMeet.Framework.Data;
using InstantMeet.Framework.Data.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InstantMeet.Web;

public class InstantMeetHub : Hub
{
    private readonly ApplicationDbContext _dbContext;

    public InstantMeetHub(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // --- Room Management Methods ---

    /// <summary>
    /// Creates a new meeting room.
    /// </summary>
    /// <param name="roomName">The desired unique name for the room.</param>
    /// <returns>True if creation successful, false otherwise (e.g., room name taken).</returns>
    public async Task<bool> CreateRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            await Clients.Caller.SendAsync("RoomCreationStatus", false, "Room name cannot be empty.");
            return false;
        }

        roomName = roomName.Trim();

        // Check if room name already exists
        if (await _dbContext.Rooms.AnyAsync(r => r.Name.ToLower() == roomName.ToLower()))
        {
            await Clients.Caller.SendAsync("RoomCreationStatus", false, $"Room '{roomName}' already exists. Please choose a different name.");
            return false;
        }

        var newRoom = new Room { Name = roomName };
        _dbContext.Rooms.Add(newRoom);
        await _dbContext.SaveChangesAsync();

        await Clients.Caller.SendAsync("RoomCreationStatus", true, $"Room '{roomName}' created successfully.");
        return true;
    }

    /// <summary>
    /// Allows a participant to join an existing meeting room.
    /// </summary>
    /// <param name="roomName">The name of the room to join.</param>
    /// <param name="userName">The name of the joining participant.</param>
    public async Task JoinRoom(string roomName, string userName)
    {
        if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(userName))
        {
            await Clients.Caller.SendAsync("JoinRoomStatus", false, "Room name and user name cannot be empty.");
            return;
        }

        roomName = roomName.Trim();
        userName = userName.Trim();

        var room = await _dbContext.Rooms
            .Include(r => r.Participants)
            .FirstOrDefaultAsync(r => r.Name.ToLower() == roomName.ToLower());

        if (room == null)
        {
            await Clients.Caller.SendAsync("JoinRoomStatus", false, $"Room '{roomName}' does not exist.");
            return;
        }

        // Add the participant to the SignalR group for the room
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        // Create new participant record
        var newParticipant = new Participant
        {
            ConnectionId = Context.ConnectionId,
            UserName = userName,
            RoomId = room.Id,
            IsCameraOn = true, // Default
            IsMicOn = true     // Default
        };
        _dbContext.Participants.Add(newParticipant);
        await _dbContext.SaveChangesAsync();

        // Notify the joining participant about existing participants
        var existingParticipants = room.Participants
            .Where(p => p.ConnectionId != Context.ConnectionId)
            .Select(p => new { p.ConnectionId, p.UserName, p.IsCameraOn, p.IsMicOn })
            .ToList();

        await Clients.Caller.SendAsync("JoinRoomStatus", true, newParticipant.ConnectionId, existingParticipants);

        // Notify all other participants in the room about the new participant
        await Clients.OthersInGroup(roomName).SendAsync("ParticipantJoined", newParticipant.ConnectionId, newParticipant.UserName, newParticipant.IsCameraOn, newParticipant.IsMicOn);

        Console.WriteLine($"Participant {userName} ({Context.ConnectionId}) joined room {roomName}");
    }

    // --- WebRTC Signaling Methods ---

    /// <summary>
    /// Sends a WebRTC offer to a specific peer.
    /// </summary>
    public async Task SendOffer(string targetConnectionId, string offer, string roomName)
    {
        Console.WriteLine($"Sending offer from {Context.ConnectionId} to {targetConnectionId} in room {roomName}");
        await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", Context.ConnectionId, offer);
    }

    /// <summary>
    /// Sends a WebRTC answer to a specific peer.
    /// </summary>
    public async Task SendAnswer(string targetConnectionId, string answer, string roomName)
    {
        Console.WriteLine($"Sending answer from {Context.ConnectionId} to {targetConnectionId} in room {roomName}");
        await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", Context.ConnectionId, answer);
    }

    /// <summary>
    /// Sends an ICE candidate to a specific peer.
    /// </summary>
    public async Task SendIceCandidate(string targetConnectionId, string candidate, string sdpMid, int sdpMLineIndex, string roomName)
    {
        Console.WriteLine($"Sending ICE candidate from {Context.ConnectionId} to {targetConnectionId} in room {roomName}");
        await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", Context.ConnectionId, candidate, sdpMid, sdpMLineIndex);
    }

    // --- Participant Media Control & Status ---

    /// <summary>
    /// Toggles a participant's camera status.
    /// </summary>
    public async Task ToggleCamera(bool isCameraOn)
    {
        var participant = await _dbContext.Participants.FirstOrDefaultAsync(p => p.ConnectionId == Context.ConnectionId);
        if (participant != null)
        {
            participant.IsCameraOn = isCameraOn;
            await _dbContext.SaveChangesAsync();
            await Clients.Group(participant.Room.Name).SendAsync("ParticipantCameraStatusChanged", participant.ConnectionId, isCameraOn);
            Console.WriteLine($"Participant {participant.UserName} camera: {isCameraOn}");
        }
    }

    /// <summary>
    /// Toggles a participant's microphone status.
    /// </summary>
    public async Task ToggleMic(bool isMicOn)
    {
        var participant = await _dbContext.Participants.FirstOrDefaultAsync(p => p.ConnectionId == Context.ConnectionId);
        if (participant != null)
        {
            participant.IsMicOn = isMicOn;
            await _dbContext.SaveChangesAsync();
            await Clients.Group(participant.Room.Name).SendAsync("ParticipantMicStatusChanged", participant.ConnectionId, isMicOn);
            Console.WriteLine($"Participant {participant.UserName} mic: {isMicOn}");
        }
    }

    /// <summary>
    /// Updates the talking status of a participant (simple indicator).
    /// </summary>
    public async Task UpdateTalkingStatus(bool isTalking)
    {
        var participant = await _dbContext.Participants
            .Include(p => p.Room)
            .FirstOrDefaultAsync(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null)
        {
            // This is a simple broadcast for mic activity.
            // No need to persist this, as it's a transient state.
            await Clients.OthersInGroup(participant.Room.Name).SendAsync("ParticipantTalking", Context.ConnectionId, isTalking);
        }
    }


    // --- Connection Lifecycle Methods ---

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var participant = await _dbContext.Participants
            .Include(p => p.Room)
            .FirstOrDefaultAsync(p => p.ConnectionId == Context.ConnectionId);

        if (participant != null)
        {
            var roomName = participant.Room.Name;
            _dbContext.Participants.Remove(participant);
            await _dbContext.SaveChangesAsync();

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).SendAsync("ParticipantLeft", Context.ConnectionId);

            Console.WriteLine($"Participant {participant.UserName} ({Context.ConnectionId}) disconnected from room {roomName}");

            // Optional: If a room becomes empty, consider deleting it
            var remainingParticipantsInRoom = await _dbContext.Participants
                .CountAsync(p => p.RoomId == participant.RoomId);

            if (remainingParticipantsInRoom == 0)
            {
                _dbContext.Rooms.Remove(participant.Room);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Room {roomName} is now empty and has been deleted.");
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}