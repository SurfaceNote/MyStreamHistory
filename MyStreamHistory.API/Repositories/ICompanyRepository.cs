namespace MyStreamHistory.API.Repositories
{
    using MyStreamHistory.API.Models;
    public interface ICompanyRepository
    {
        Task<Company> CreateCompanyAsync(Company company);

        Task DeleteCompanyAsync(int id);

        Task<IEnumerable<Company>> GetCompaniesAsync();

        Task<Company?> GetCompanyByIdAsync(int id);

        Task<Company?> GetCompanyByIgdbIdAsync(int igdbId);

        Task<Company?> GetCompanyBySlugAsync(string slug);

        Task UpdateCompanyAsync(Company company);
    }
}
