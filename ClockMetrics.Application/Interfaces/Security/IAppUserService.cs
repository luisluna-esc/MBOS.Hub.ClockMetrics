using ClockMetrics.Application.Dtos.Security;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.QueryFilters.Pagination;
using ClockMetrics.Core.QueryFilters.Security;

namespace ClockMetrics.Application.Interfaces.Security;

public interface IAppUserService
{
    Task<ResponseGetObject> GetAllAppUsers(PaginationQueryFilter paginationQueryFilter, AppUserQueryFilter appUserQueryFilter);
    Task<ResponsePost> InsertAppUser(AppUserDto appUserDto);
    Task<ResponsePost> UpdateAppUser(long id, AppUserDto appUserDto);
    Task<ResponsePost> DeleteAppUser(long id);
}
