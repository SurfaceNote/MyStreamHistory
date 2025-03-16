namespace MyStreamHistory.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using MyStreamHistory.API.DTOs;
    using MyStreamHistory.API.Models;
    using MyStreamHistory.API.Repositories;

    [Route("api/[controller]")]
    [ApiController]
    public class CompaniesController : ControllerBase
    {
        private readonly ICompanyRepository _repository;

        public CompaniesController(ICompanyRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CompanyDTO>>> GetCompaniesDTO()
        {
            var companies = await _repository.GetCompaniesAsync();

            var companiesDTO = companies.Select(c => new CompanyDTO
            {
                Slug = c.Slug,
                Name = c.Name
            });

            return Ok(companiesDTO);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<IEnumerable<CompanyDTO>>> GetCompanyDTO(int id)
        {
            var company = await _repository.GetCompanyByIdAsync(id);

            if (company == null)
            {
                return NotFound();
            }

            var companyDTO = new CompanyDTO
            {
                Slug = company.Slug,
                Name = company.Name
            };

            return Ok(companyDTO);
        }

    }
}
