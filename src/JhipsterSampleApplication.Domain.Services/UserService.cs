using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JHipsterNet.Core.Pagination.Extensions;
using JhipsterSampleApplication.Crosscutting.Constants;
using JhipsterSampleApplication.Crosscutting.Exceptions;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using JhipsterSampleApplication.Crosscutting.Utilities;
using LanguageExt.UnitsOfMeasure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JhipsterSampleApplication.Domain.Services;

public class UserService : IUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserService> _log;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly RoleManager<Role> _roleManager;
    private readonly UserManager<User> _userManager;

    public UserService(ILogger<UserService> log, UserManager<User> userManager,
        IPasswordHasher<User> passwordHasher, RoleManager<Role> roleManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _log = log;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _roleManager = roleManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public virtual async Task<User> CreateUser(User userToCreate)
    {
        var user = new User
        {
            UserName = userToCreate.Login?.ToLower(),
            FirstName = userToCreate.FirstName,
            LastName = userToCreate.LastName,
            Email = userToCreate.Email?.ToLower(),
            ImageUrl = userToCreate.ImageUrl,
            LangKey = userToCreate.LangKey ?? Constants.DefaultLangKey,
            PasswordHash = _userManager.PasswordHasher.HashPassword(null!, RandomUtil.GeneratePassword()),
            ResetKey = RandomUtil.GenerateResetKey(),
            ResetDate = DateTime.Now,
            Activated = true
        };
        await _userManager.CreateAsync(user);
        await CreateUserRoles(user, userToCreate.UserRoles?.Select(iur => iur.Role!.Name!).ToHashSet()!);
        _log.LogDebug($"Created Information for User: {user}");
        return user!;
    }

    public async Task<IPage<User>> GetAllManagedUsers(IPageable pageable)
    {
        return await _userManager.Users
            .Include(it => it.UserRoles!)
            .ThenInclude(r => r.Role!)
            .UsePageableAsync(pageable);
    }

    public async Task<IPage<User?>> GetAllPublicUsers(IPageable pageable)
    {
        return (await _userManager.Users
            .Where(it => it.Activated == true && !string.IsNullOrEmpty(it.Id))
            .UsePageableAsync(pageable))!;
    }

    public async Task<User?> GetByLogin(string login)
    {
        return await _userManager.Users
            .Where(user => user.Login! == login)
            .Include(it => it.UserRoles!)
            .ThenInclude(r => r.Role!)
            .SingleOrDefaultAsync();
    }

    public virtual async Task<User?> UpdateUser(User userToUpdate)
    {
        //TODO use Optional
        var user = (await _userManager.FindByIdAsync(userToUpdate!.Id))!;
        user.Login = userToUpdate.Login!.ToLower();
        user.UserName = userToUpdate.Login!.ToLower();
        user.FirstName = userToUpdate.FirstName!;
        user.LastName = userToUpdate.LastName!;
        user.Email = userToUpdate.Email!;
        user.ImageUrl = userToUpdate.ImageUrl!;
        user.Activated = userToUpdate.Activated!;
        user.LangKey = userToUpdate.LangKey!;
        await _userManager.UpdateAsync(user);
        var roleNames = userToUpdate.UserRoles?
                .Select(iur => iur.Role?.Name)   // string?
                .Where(n => n is not null)       // filter nulls
                .Select(n => n!)                 // cast to string
                .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        await UpdateUserRoles(user, roleNames);
        return user;
    }

    public virtual async Task<User?> CompletePasswordReset(string newPassword, string key)
    {
        _log.LogDebug($"Reset user password for reset key {key}");
        var user = await _userManager.Users.SingleOrDefaultAsync(it => it.ResetKey == key);
        if (user == null || user.ResetDate <= DateTime.Now.Subtract(86400.Seconds())) return null;
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.ResetKey = null;
        user.ResetDate = null;
        await _userManager.UpdateAsync(user);
        return user;
    }

    public virtual async Task<User?> RequestPasswordReset(string mail)
    {
        var user = await _userManager.FindByEmailAsync(mail);
        if (user == null) return null;
        user.ResetKey = RandomUtil.GenerateResetKey();
        user.ResetDate = DateTime.Now;
        await _userManager.UpdateAsync(user);
        return user;
    }

    public virtual async Task ChangePassword(string currentClearTextPassword, string newPassword)
    {
        var userName = _userManager.GetUserName(_httpContextAccessor.HttpContext.User);
        var user = await _userManager.FindByNameAsync(userName!);
        if (user != null)
        {
            var currentEncryptedPassword = user.PasswordHash!;
            var isInvalidPassword =
                _passwordHasher.VerifyHashedPassword(user, currentEncryptedPassword, currentClearTextPassword) !=
                PasswordVerificationResult.Success;
            if (isInvalidPassword) throw new InvalidPasswordException();

            var encryptedPassword = _passwordHasher.HashPassword(user, newPassword);
            user.PasswordHash = encryptedPassword;
            await _userManager.UpdateAsync(user);
            _log.LogDebug($"Changed password for User: {user}");
        }
    }

    public virtual async Task<User?> ActivateRegistration(string key)
    {
        _log.LogDebug($"Activating user for activation key {key}");
        var user = await _userManager.Users.SingleOrDefaultAsync(it => it.ActivationKey == key);

        if (user == null) return null;
        user.Activated = true;
        user.ActivationKey = null;
        await _userManager.UpdateAsync(user);
        _log.LogDebug($"Activated user: {user}");
        return user;
    }


    public virtual async Task<User> RegisterUser(User userToRegister, string password)
    {
        var existingUser = await _userManager.FindByNameAsync(userToRegister.Login!.ToLower());
        if (existingUser != null)
        {
            var removed = await RemoveNonActivatedUser(existingUser);
            if (!removed) throw new LoginAlreadyUsedException();
        }

        existingUser = _userManager.Users.FirstOrDefault(it => it.Email == userToRegister.Email);
        if (existingUser != null)
        {
            var removed = await RemoveNonActivatedUser(existingUser);
            if (!removed) throw new EmailAlreadyUsedException();
        }

        var newUser = new User
        {
            Login = userToRegister.Login,
            // new user gets initially a generated password
            PasswordHash = _passwordHasher.HashPassword(null!, password),
            FirstName = userToRegister.FirstName!,
            LastName = userToRegister.LastName!,
            Email = userToRegister.Email!.ToLowerInvariant(),
            ImageUrl = userToRegister.ImageUrl!,
            LangKey = userToRegister.LangKey!,
            // new user is not active
            Activated = false,
            // new user gets registration key
            ActivationKey = RandomUtil.GenerateActivationKey()
            //TODO manage authorities
        };
        await _userManager.CreateAsync(newUser);
        _log.LogDebug($"Created Information for User: {newUser}");
        return newUser;
    }

    public virtual async Task UpdateUser(string firstName, string lastName, string email, string langKey, string imageUrl)
    {
        var userName = _userManager.GetUserName(_httpContextAccessor.HttpContext.User)!;
        var user = await _userManager.FindByNameAsync(userName);
        if (user != null)
        {
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.LangKey = langKey;
            user.ImageUrl = imageUrl;
            await _userManager.UpdateAsync(user);
            _log.LogDebug($"Changed Information for User: {user}");
        }
    }

    public virtual async Task<User?> GetUserWithUserRoles()
    {
        var userName = _userManager.GetUserName(_httpContextAccessor.HttpContext.User);
        if (userName == null) return null;
        return await GetUserWithUserRolesByName(userName);
    }


    public virtual IEnumerable<string> GetAuthorities()
    {
        return _roleManager.Roles.Select(it => it.Name!).AsQueryable();
    }

    public virtual async Task DeleteUser(string login)
    {
        var user = await _userManager.FindByNameAsync(login);
        if (user != null)
        {
            await DeleteUserRoles(user);
            await _userManager.DeleteAsync(user);
            _log.LogDebug($"Deleted User: {user}");
        }
    }

    private async Task<User?> GetUserWithUserRolesByName(string name)
    {
        return await _userManager.Users
            .Include(it => it.UserRoles!)
            .ThenInclude(r => r.Role!)
            .SingleOrDefaultAsync(it => it.UserName == name)!;
    }

    private async Task<bool> RemoveNonActivatedUser(User existingUser)
    {
        if (existingUser.Activated) return false;

        await _userManager.DeleteAsync(existingUser);
        return true;
    }

    private async Task CreateUserRoles(User user, IEnumerable<string?> roles)
    {
        if (roles == null || !roles.Any()) return;

        foreach (var role in roles.Where(r=>r != null)) await _userManager.AddToRoleAsync(user, role!);
    }

    private async Task UpdateUserRoles(User user, IEnumerable<string?> roles)
    {
        var userRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = userRoles.Except(roles).ToArray();
        var rolesToAdd = roles.Except(userRoles).Distinct().ToArray();
        await _userManager.RemoveFromRolesAsync(user, rolesToRemove!);
        await _userManager.AddToRolesAsync(user, rolesToAdd!);
    }

    private async Task DeleteUserRoles(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, roles);
    }
}
