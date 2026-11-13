using ClockMetrics.Application.Dtos.Security;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.QueryFilters.Pagination;
using ClockMetrics.Core.QueryFilters.Security;

namespace ClockMetrics.Application.Interfaces.Security;

public interface IRoleService
{
    Task<ResponseGetObject> GetAllRoles(PaginationQueryFilter paginationQueryFilter, RoleQueryFilter roleQueryFilter);
    Task<ResponsePost> InsertRole(RoleDto roleDto);
    Task<ResponsePost> UpdateRole(long id, RoleDto roleDto);
    Task<ResponsePost> DeleteRole(long id);
}
