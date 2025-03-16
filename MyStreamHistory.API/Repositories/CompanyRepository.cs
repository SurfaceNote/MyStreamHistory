namespace MyStreamHistory.API.Repositories
{
    using Microsoft.EntityFrameworkCore;
    using MyStreamHistory.API.Data;
    using MyStreamHistory.API.Models;

    public class CompanyRepository : ICompanyRepository
    {
        private readonly AppDbContext _appDbContext;

        public CompanyRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<Company> CreateCompanyAsync(Company company)
        {
            _appDbContext.Companies.Add(company);
            await _appDbContext.SaveChangesAsync();
            return company;
        }

        public async Task DeleteCompanyAsync(int id)
        {
            var company = await _appDbContext.Companies.FindAsync(id);
            if (company != null)
            {
                _appDbContext.Companies.Remove(company);
                await _appDbContext.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Company>> GetCompaniesAsync()
        {
            return await _appDbContext.Companies.ToListAsync();
        }

        public async Task<Company?> GetCompanyByIdAsync(int id)
        {
            return await _appDbContext.Companies.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Company?> GetCompanyByIgdbIdAsync(int igdbId)
        {
            return await _appDbContext.Companies.FirstOrDefaultAsync(x => x.IgdbId == igdbId);
        }

        public async Task<Company?> GetCompanyBySlugAsync(string slug)
        {
            return await _appDbContext.Companies.FirstOrDefaultAsync(x => x.Slug == slug);
        }

        public async Task UpdateCompanyAsync(Company company)
        {
            _appDbContext.Entry(company).State = EntityState.Modified;
            await _appDbContext.SaveChangesAsync();
        }
    }
}
