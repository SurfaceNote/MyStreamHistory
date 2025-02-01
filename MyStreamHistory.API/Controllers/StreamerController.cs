namespace MyStreamHistory.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using MyStreamHistory.API.DTOs;
    using MyStreamHistory.API.Models;
    using MyStreamHistory.API.Repositories;

    [Route("api/[controller]")]
    [ApiController]
    public class StreamerController : ControllerBase
    {
        private readonly IStreamerRepository _repository;

        public StreamerController(IStreamerRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("full")]
        public async Task<ActionResult<IEnumerable<Streamer>>> GetStreamers()
        {
            var streamers = await _repository.GetStreamersAsync();
            return Ok(streamers);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StreamerDTO>>> GetStreamersDTO()
        {
            var streamers = await _repository.GetStreamersAsync();

            var streamersDTO = streamers.Select(s => new StreamerDTO
            {
                TwitchId = s.TwitchId,
                ChannelName = s.ChannelName,
                BroadcasterType = s.BroadcasterType,
                LogoUser = s.LogoUser
            });

            return Ok(streamersDTO);
        }

        [HttpGet("full/{id}")]
        public async Task<ActionResult<Streamer>> GetStreamer(int id)
        {
            var streamer = await _repository.GetStreamerByIdAsync(id);

            if (streamer == null)
            {
                return NotFound();
            }

            return Ok(streamer);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StreamerDTO>> GetStreamerDTO(int id)
        {
            var streamer = await _repository.GetStreamerByIdAsync(id);

            if (streamer == null)
            {
                return NotFound();
            }

            var streamerDTO = new StreamerDTO
            {
                TwitchId = streamer.TwitchId,
                ChannelName = streamer.ChannelName,
                BroadcasterType = streamer.BroadcasterType,
            };
            return Ok(streamerDTO);
        }

        [HttpPost]
        public async Task<ActionResult<Streamer>> PostStreamer(Streamer streamer)
        {
            var createdStreamer = await _repository.CreateStreamerAsync(streamer);
            return CreatedAtAction(nameof(GetStreamer), new { id = createdStreamer.Id }, createdStreamer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutStreamer(int id, Streamer streamer)
        {
            if (id != streamer.Id) 
            {
                return BadRequest();
            }

            await _repository.UpdateStreamerAsync(streamer);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStreamer(int id)
        {
            var streamer = await _repository.GetStreamerByIdAsync(id);
            if (streamer == null)
            {
                return NotFound();
            }

            await _repository.DeleteStreamerAsync(id);

            return NoContent();
        }
    }
}
