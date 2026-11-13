using ClockMetrics.Application.Dtos.Security;
using ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;
using ClockMetrics.Core.QueryFilters.Pagination;
using ClockMetrics.Core.QueryFilters.Security;

namespace ClockMetrics.Application.Interfaces.Security;

public interface IMenuService
{
    Task<ResponseGetObject> GetAllMenus(PaginationQueryFilter paginationQueryFilter, MenuQueryFilter menuQueryFilter);
    Task<ResponsePost> InsertMenu(MenuDto menuDto);
    Task<ResponsePost> UpdateMenu(long id, MenuDto menuDto);
    Task<ResponsePost> DeleteMenu(long id);
    Task<ResponseGetObject> GetMyMenu();
}
