using System.Collections.Generic;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using System.Linq;
using System;

namespace JhipsterSampleApplication.Domain.Services
{
    public class NamedQueryService : INamedQueryService
    {
        private readonly INamedQueryRepository _namedQueryRepository;
        private readonly IUserService _userService;

        public NamedQueryService(INamedQueryRepository namedQueryRepository, IUserService userService)
        {
            _namedQueryRepository = namedQueryRepository;
            _userService = userService;
        }

        public async Task<NamedQuery> Save(NamedQuery namedQuery)
        {
            namedQuery.Name = namedQuery.Name.ToUpper();
            namedQuery.Owner = string.IsNullOrEmpty(namedQuery.Owner) ? namedQuery.Owner : namedQuery.Owner.ToLower().Replace("global","GLOBAL").Replace("system","SYSTEM");
            
            var currentUser = await _userService.GetUserWithUserRoles();
            // If no current user (during startup), treat as admin for initialization
            bool isAdmin = currentUser == null || (currentUser.UserRoles?.Any(ur => ur.Role != null && ur.Role.Name == "ROLE_ADMIN") == true);

            // Admin logic
            if (isAdmin)
            {
                // Admin can update any record, including GLOBAL/SYSTEM, as long as no duplicate
                if (namedQuery.Owner == "SYSTEM")
                {
                    namedQuery.Owner = "GLOBAL";
                    namedQuery.IsSystem = true;
                }
                else if (namedQuery.Owner == "GLOBAL")
                {
                    namedQuery.IsSystem = false;
                }
                else
                {
                    namedQuery.IsSystem = null;
                    namedQuery.Owner = string.IsNullOrEmpty(namedQuery.Owner) ? (currentUser?.Login ?? namedQuery.Owner) : namedQuery.Owner;
                }
                NamedQuery? existing = await FindByNameAndOwner(namedQuery.Name, namedQuery.Owner.Replace("SYSTEM", "GLOBAL"));
                if (existing != null && existing.Id != namedQuery.Id && existing.Owner.Replace("SYSTEM", "GLOBAL") == namedQuery.Owner.Replace("SYSTEM", "GLOBAL"))
                {
                    throw new InvalidOperationException("A query by that name already exists");
                }
                if (existing != null && existing.Owner.Replace("SYSTEM","GLOBAL") != namedQuery.Owner.Replace("SYSTEM", "GLOBAL"))
                {
                    namedQuery.Id = 0; // force insert
                }
            }
            else
            {
                NamedQuery? existing = await  FindByNameAndOwner(namedQuery.Name, currentUser!.Login!);
                if (existing != null && existing.Id != namedQuery.Id && existing.Owner != "GLOBAL")
                {
                    throw new InvalidOperationException("A query by that name already exists");
                }
                // Non-admin logic
                if (string.IsNullOrEmpty(namedQuery.Owner) || namedQuery.Owner == currentUser.Login)
                {
                    // User is creating or updating their own query
                    namedQuery.IsSystem = null;
                    namedQuery.Owner = currentUser.Login!;
                }
                else if (namedQuery.Owner == "GLOBAL" || namedQuery.IsSystem == true)
                {
                    // User is trying to update a GLOBAL or SYSTEM query: create a new query for the user
                    namedQuery.Id = 0; // Force insert
                    namedQuery.IsSystem = null;
                    namedQuery.Owner = currentUser.Login!;
                }
                else
                {
                    // User is trying to update someone else's query: not allowed
                    throw new UnauthorizedAccessException("You are not allowed to update this named query");
                }
            }

            await _namedQueryRepository.CreateOrUpdateAsync(namedQuery);
            await _namedQueryRepository.SaveChangesAsync();
            return namedQuery;
        }

        public async Task<IPage<NamedQuery>> FindAll(IPageable pageable)
        {
            var result = await _namedQueryRepository.FindAllNamedQueries(pageable);
            foreach (var query in result.Content)
            {
                if (query.IsSystem == true)
                {
                    query.Owner = "SYSTEM";
                }
            }
            return result;
        }

        public async Task<NamedQuery?> FindOne(long id)
        {
            var result = await _namedQueryRepository.FindOne(id);
            if (result != null && result.IsSystem == true)
            {
                result.Owner = "SYSTEM";
            }
            return result;
        }

        public async Task Delete(long id)
        {
            var namedQuery = await _namedQueryRepository.FindOne(id);
            if (namedQuery == null)
            {
                throw new InvalidOperationException($"NamedQuery with id {id} not found");
            }

            var currentUser = await _userService.GetUserWithUserRoles();
            if (currentUser == null)
                throw new InvalidOperationException("Current user not found.");
            var isAdmin = currentUser.UserRoles?.Any(ur => ur.Role != null && ur.Role.Name == "ROLE_ADMIN") == true;

            // Only check for GLOBAL/SYSTEM if user is not admin
            if (!isAdmin && (namedQuery.Owner == "GLOBAL" || namedQuery.IsSystem == true))
            {
                throw new UnauthorizedAccessException("Cannot delete SYSTEM or GLOBAL named queries");
            }

            await _namedQueryRepository.DeleteByIdAsync(id);
            await _namedQueryRepository.SaveChangesAsync();
        }

        public async Task<IEnumerable<NamedQuery>> FindByOwner(string owner)
        {
            List<NamedQuery> owners = [.. (await _namedQueryRepository.FindByOwnerAsync(owner))];
            List<string> ownerNames = owners.Select(o=>o.Name).ToList();
            List<NamedQuery> global = [.. (await _namedQueryRepository.FindByOwnerAsync("GLOBAL"))];
            owners.AddRange(global.Where(g => !ownerNames.Contains(g.Name)));
            foreach (var query in owners)
            {
                if (query.IsSystem == true)
                {
                    query.Owner = "SYSTEM";
                }
            }
            return owners;
        }

        public async Task<IEnumerable<NamedQuery>> FindByName(string name)
        {
            var result = await _namedQueryRepository.QueryHelper()
                .Filter(nq => nq.Name == name)
                .GetAllAsync();
            foreach (var query in result)
            {
                if (query.IsSystem == true)
                {
                    query.Owner = "SYSTEM";
                }
            }
            return result;
        }

        public async Task<NamedQuery?> FindByNameAndOwner(string name, string owner)
        {
            var currentUser = await _userService.GetUserWithUserRoles();
            // Only check authorization if we have a current user (i.e. not during startup)
            if (currentUser != null)
            {
                var isAdmin = currentUser.UserRoles?.Any(ur => ur.Role != null && ur.Role.Name == "ROLE_ADMIN") == true;
                if (!isAdmin && !string.IsNullOrEmpty(owner) && owner != currentUser.Login)
                {
                    throw new UnauthorizedAccessException("You are not allowed to request queries by another owner");
                }
            }
            var found = await _namedQueryRepository.QueryHelper()
                .Filter(nq => nq.Name == name && nq.Owner == owner)
                .GetAllAsync();
            if (!found.Any()){
                found = await _namedQueryRepository.QueryHelper()
                    .Filter(nq => nq.Name == name && nq.Owner == "GLOBAL")
                    .GetAllAsync();                
            }
            if (!found.Any())
            {
                return null;
            }
            if (found.ToList().Count > 1)
            {
                throw new InvalidOperationException("Duplicate found in database.");
            }
            var result = found.ToList()[0];
            if (result.IsSystem == true)
            {
                result.Owner = "SYSTEM";
            }
            return result;
        }
    }
} 