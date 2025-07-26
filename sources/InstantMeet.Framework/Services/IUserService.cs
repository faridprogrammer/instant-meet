using System;

namespace InstantMeet.Framework.Services;

public interface IUserService
{
    Task<bool> AddUser(string userName, string password);

    Task<bool> DeleteUser(string userName);

    Task<bool> ValidateCredentials(string userName, string password);
}
