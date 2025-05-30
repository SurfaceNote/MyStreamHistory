namespace MyStreamHistory.API.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using MyStreamHistory.API.DTOs;
    using MyStreamHistory.API.Models;
    using MyStreamHistory.API.Repositories;

    [Route("api/[controller]")]
    [ApiController]
    public class StreamerController : ControllerBase
    {
        private readonly IUserRepository _repository;

        public StreamerController(IUserRepository repository)
        {
            _repository = repository;
        }

        [Authorize]
        [HttpGet("full")]
        public async Task<ActionResult<IEnumerable<User>>> GetStreamers()
        {
            var streamers = await _repository.GetStreamersAsync();
            return Ok(streamers);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<StreamerDTO>>> GetStreamersDTO()
        {
            var streamers = await _repository.GetStreamersAsync();

            var streamersDTO = streamers.Select(s => new StreamerDTO
            {
                TwitchId = s.TwitchId,
                Login = s.Login,
                DisplayName = s.DisplayName,
                BroadcasterType = s.BroadcasterType,
                LogoUser = s.LogoUser
            });

            return Ok(streamersDTO);
        }

        [HttpGet("full/{id}")]
        public async Task<ActionResult<User>> GetStreamer(int id)
        {
            var streamer = await _repository.GetUserByIdAsync(id);

            if (streamer == null)
            {
                return NotFound();
            }

            return Ok(streamer);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<StreamerDTO>> GetStreamerDTO(int id)
        {
            var streamer = await _repository.GetUserByIdAsync(id);

            if (streamer == null)
            {
                return NotFound();
            }

            var streamerDTO = new StreamerDTO
            {
                TwitchId = streamer.TwitchId,
                Login = streamer.Login,
                BroadcasterType = streamer.BroadcasterType,
            };
            return Ok(streamerDTO);
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostStreamer(User streamer)
        {
            var createdStreamer = await _repository.CreateUserAsync(streamer);
            return CreatedAtAction(nameof(GetStreamer), new { id = createdStreamer.Id }, createdStreamer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutStreamer(int id, User streamer)
        {
            if (id != streamer.Id) 
            {
                return BadRequest();
            }

            await _repository.UpdateUserAsync(streamer);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStreamer(int id)
        {
            var streamer = await _repository.GetUserByIdAsync(id);
            if (streamer == null)
            {
                return NotFound();
            }

            await _repository.DeleteStreamerAsync(id);

            return NoContent();
        }
    }
}
