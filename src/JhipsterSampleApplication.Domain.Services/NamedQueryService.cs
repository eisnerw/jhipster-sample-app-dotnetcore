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
            var currentUser = await _userService.GetUserWithUserRoles();
            if (currentUser == null)
                throw new InvalidOperationException("Current user not found.");
            var isAdmin = currentUser.UserRoles?.Any(ur => ur.Role != null && ur.Role.Name == "ROLE_ADMIN") == true;

            var existing = await _namedQueryRepository.FindOne(namedQuery.Id);

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
                }
            }
            else
            {
                // Non-admin logic
                if (string.IsNullOrEmpty(namedQuery.Owner) || namedQuery.Owner == currentUser.Login)
                {
                    // User is creating or updating their own query
                    namedQuery.Owner = currentUser.Login ?? throw new InvalidOperationException("User login is null.");
                    namedQuery.IsSystem = null;
                }
                if (namedQuery.Owner == "GLOBAL" || namedQuery.IsSystem == true)
                {
                    // User is trying to update a GLOBAL or SYSTEM query: create a new query for the user
                    namedQuery.Id = 0; // Force insert
                    namedQuery.Owner = currentUser.Login ?? throw new InvalidOperationException("User login is null.");
                    namedQuery.IsSystem = null;
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
            List<NamedQuery> owners = (await _namedQueryRepository.FindByOwnerAsync(owner)).ToList();
            List<NamedQuery> global = (await _namedQueryRepository.FindByOwnerAsync("GLOBAL")).ToList();
            owners.AddRange(global.Where(g => !owners.Any(o => o.Name == g.Name)));
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

        public async Task<IEnumerable<NamedQuery>> FindByNameAndOwner(string name, string owner)
        {
            var result = await _namedQueryRepository.QueryHelper()
                .Filter(nq => nq.Name == name && nq.Owner == owner)
                .GetAllAsync();
            if (!result.Any()){
                result = await _namedQueryRepository.QueryHelper()
                    .Filter(nq => nq.Name == name && nq.Owner == "GLOBAL")
                    .GetAllAsync();                
            }
            foreach (var query in result)
            {
                if (query.IsSystem == true)
                {
                    query.Owner = "SYSTEM";
                }
            }
            return result;
        }
    }
} 