
using AutoMapper;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using Jhipster.Domain;
using Jhipster.Crosscutting.Exceptions;
using Jhipster.Dto;
using Jhipster.Domain.Services.Interfaces;
using Jhipster.Web.Extensions;
using Jhipster.Web.Filters;
using Jhipster.Web.Rest.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Jhipster.Controllers
{
    [Authorize]
    [Route("api")]
    [ApiController]
    public class BirthdayController : ControllerBase
    {
        private const string EntityName = "birthday";
        private readonly IMapper _mapper;
        private readonly IBirthdayService _birthdayService;
        private readonly ILogger<BirthdayController> _log;

        public BirthdayController(ILogger<BirthdayController> log,
            IMapper mapper,
            IBirthdayService birthdayService)
        {
            _log = log;
            _mapper = mapper;
            _birthdayService = birthdayService;
        }

        [HttpPost("birthdays")]
        [ValidateModel]
        public async Task<ActionResult<BirthdayDto>> CreateBirthday([FromBody] BirthdayDto birthdayDto)
        {
            _log.LogDebug($"REST request to save Birthday : {birthdayDto}");
            if (birthdayDto.Id != 0)
                throw new BadRequestAlertException("A new birthday cannot already have an ID", EntityName, "idexists");

            Birthday birthday = _mapper.Map<Birthday>(birthdayDto);
            await _birthdayService.Save(birthday);
            return CreatedAtAction(nameof(GetBirthday), new { id = birthday.Id }, birthday)
                .WithHeaders(HeaderUtil.CreateEntityCreationAlert(EntityName, birthday.Id.ToString()));
        }

        [HttpPut("birthdays")]
        [ValidateModel]
        public async Task<IActionResult> UpdateBirthday([FromBody] BirthdayDto birthdayDto)
        {
            _log.LogDebug($"REST request to update Birthday : {birthdayDto}");
            if (birthdayDto.Id == 0) throw new BadRequestAlertException("Invalid Id", EntityName, "idnull");
            Birthday birthday = _mapper.Map<Birthday>(birthdayDto);
            await _birthdayService.Save(birthday);
            return Ok(birthday)
                .WithHeaders(HeaderUtil.CreateEntityUpdateAlert(EntityName, birthday.Id.ToString()));
        }

        [HttpGet("birthdays")]
        public async Task<ActionResult<IEnumerable<BirthdayDto>>> GetAllBirthdays(IPageable pageable)
        {
            _log.LogDebug("REST request to get a page of Birthdays");
            var result = await _birthdayService.FindAll(pageable);
            var page = new Page<BirthdayDto>(result.Content.Select(entity => _mapper.Map<BirthdayDto>(entity)).ToList(), pageable, result.TotalElements);
            return Ok(((IPage<BirthdayDto>)page).Content).WithHeaders(page.GeneratePaginationHttpHeaders());
        }

        [HttpGet("birthdays/{id}")]
        public async Task<IActionResult> GetBirthday([FromRoute] long id)
        {
            _log.LogDebug($"REST request to get Birthday : {id}");
            var result = await _birthdayService.FindOne(id);
            BirthdayDto birthdayDto = _mapper.Map<BirthdayDto>(result);
            return ActionResultUtil.WrapOrNotFound(birthdayDto);
        }

        [HttpDelete("birthdays/{id}")]
        public async Task<IActionResult> DeleteBirthday([FromRoute] long id)
        {
            _log.LogDebug($"REST request to delete Birthday : {id}");
            await _birthdayService.Delete(id);
            return Ok().WithHeaders(HeaderUtil.CreateEntityDeletionAlert(EntityName, id.ToString()));
        }
    }
}
