using HRM.Modules.Personnel.Application.Services;

namespace HRM.Modules.Personnel.Infrastructure.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public OrganizationService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> IsValidAssignmentAsync(Guid companyId, Guid departmentId, Guid positionId)
        {
            var client = _httpClientFactory.CreateClient("OrganizationApi");
            var response = await client.GetAsync($"/api/organization/positions/validate?companyId={companyId}&departmentId={departmentId}&positionId={positionId}");
            return response.IsSuccessStatusCode;
        }
    }
}
