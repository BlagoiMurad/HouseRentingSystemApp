using HouseRentingSystemApi.Data;
using HouseRentingSystemApi.Data.Entities;
using HouseRentingSystemApi.Models;
using HouseRentingSystemApi.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HouseRentingSystemApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HouseController : ControllerBase
    {
        private readonly AppDbContext context;

        public HouseController(AppDbContext context)
        {
            this.context = context;
        }

        // ─── ПУБЛИЧНИ ENDPOINTS (без авторизация) ───────────────────────────

        /// <summary>Връща всички къщи</summary>
        [HttpGet("All")]
        [Produces(typeof(IEnumerable<HouseDetailModel>))]
        public async Task<IActionResult> GetAll()
        {
            var model = await context.Houses
                .AsNoTracking()
                .Select(h => new HouseDetailModel
                {
                    Id = h.Id,
                    Title = h.Title,
                    Address = h.Address,
                    ImageUrl = h.ImageUrl,
                    Description = h.Description,
                    PricePerMonth = h.PricePerMonth,
                    Category = (CategoryViewEnum)h.CategoryId,
                    IsRented = h.RenterId != null
                })
                .ToListAsync();

            return Ok(model);
        }

        /// <summary>Връща конкретна къща по ID</summary>
        [HttpGet("{id}")]
        [Produces(typeof(HouseDetailModel))]
        public async Task<IActionResult> GetById(int id)
        {
            var house = await context.Houses
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == id);

            if (house == null)
                return NotFound();

            return Ok(new HouseDetailModel
            {
                Id = house.Id,
                Title = house.Title,
                Address = house.Address,
                ImageUrl = house.ImageUrl,
                Description = house.Description,
                PricePerMonth = house.PricePerMonth,
                Category = (CategoryViewEnum)house.CategoryId,
                IsRented = house.RenterId != null
            });
        }

        // ─── AGENT ENDPOINTS (само агенти) ──────────────────────────────────

        /// <summary>Агент създава нова къща</summary>
        [Authorize(Roles = "Agent")]
        [HttpPost]
        [Produces(typeof(HouseDetailModel))]
        public async Task<IActionResult> Create([FromBody] HouseDetailModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var newHouse = new House
            {
                Title = model.Title,
                Address = model.Address,
                Description = model.Description,
                ImageUrl = model.ImageUrl,
                PricePerMonth = model.PricePerMonth,
                UserId = agentId   // записваме кой агент е създал обявата
            };

            var category = await GetOrCreateCategory(model.Category.ToString());
            newHouse.CategoryId = category.Id;

            context.Houses.Add(newHouse);
            await context.SaveChangesAsync();

            return Created($"api/House/{newHouse.Id}", new HouseDetailModel
            {
                Id = newHouse.Id,
                Title = newHouse.Title,
                Address = newHouse.Address,
                ImageUrl = newHouse.ImageUrl,
                Description = newHouse.Description,
                PricePerMonth = newHouse.PricePerMonth,
                Category = model.Category,
                IsRented = false
            });
        }

        /// <summary>Агент редактира съществуваща къща</summary>
        [Authorize(Roles = "Agent")]
        [HttpPut("{id}")]
        [Produces(typeof(HouseDetailModel))]
        public async Task<IActionResult> Edit([FromRoute] int id, [FromBody] HouseDetailModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var house = await context.Houses.FirstOrDefaultAsync(h => h.Id == id);

            if (house == null)
                return NotFound("Къщата не е намерена.");

            house.Title = model.Title;
            house.Address = model.Address;
            house.ImageUrl = model.ImageUrl;
            house.Description = model.Description;
            house.PricePerMonth = model.PricePerMonth;

            var category = await GetOrCreateCategory(model.Category.ToString());
            house.CategoryId = category.Id;

            await context.SaveChangesAsync();

            return Ok(new HouseDetailModel
            {
                Id = house.Id,
                Title = house.Title,
                Address = house.Address,
                ImageUrl = house.ImageUrl,
                Description = house.Description,
                PricePerMonth = house.PricePerMonth,
                Category = (CategoryViewEnum)house.CategoryId,
                IsRented = house.RenterId != null
            });
        }

        /// <summary>Агент трие къща</summary>
        [Authorize(Roles = "Agent")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            var house = await context.Houses.FirstOrDefaultAsync(h => h.Id == id);

            if (house == null)
                return NotFound("Къщата не е намерена.");

            context.Houses.Remove(house);
            await context.SaveChangesAsync();

            return Ok(new { message = $"Къща с id {id} беше изтрита успешно." });
        }

        // ─── CLIENT ENDPOINTS (само клиенти) ────────────────────────────────

        /// <summary>Клиент наема свободна къща</summary>
        [Authorize(Roles = "Client")]
        [HttpPost("{id}/rent")]
        public async Task<IActionResult> Rent([FromRoute] int id)
        {
            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var house = await context.Houses.FirstOrDefaultAsync(h => h.Id == id);

            if (house == null)
                return NotFound("Къщата не е намерена.");

            if (house.RenterId != null)
                return BadRequest("Тази къща вече е наета.");

            house.RenterId = clientId;
            await context.SaveChangesAsync();

            return Ok(new { message = $"Успешно наехте къща '{house.Title}'." });
        }

        /// <summary>Клиент освобождава наета от него къща</summary>
        [Authorize(Roles = "Client")]
        [HttpPost("{id}/release")]
        public async Task<IActionResult> Release([FromRoute] int id)
        {
            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var house = await context.Houses.FirstOrDefaultAsync(h => h.Id == id);

            if (house == null)
                return NotFound("Къщата не е намерена.");

            if (house.RenterId == null)
                return BadRequest("Тази къща не е наета.");

            if (house.RenterId != clientId)
                return Forbid(); // друг клиент е наел тази къща

            house.RenterId = null;
            await context.SaveChangesAsync();

            return Ok(new { message = $"Успешно освободихте къща '{house.Title}'." });
        }

        /// <summary>Клиент вижда наетите от него къщи</summary>
        [Authorize(Roles = "Client")]
        [HttpGet("MyRented")]
        [Produces(typeof(IEnumerable<HouseDetailModel>))]
        public async Task<IActionResult> MyRented()
        {
            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var houses = await context.Houses
                .AsNoTracking()
                .Where(h => h.RenterId == clientId)
                .Select(h => new HouseDetailModel
                {
                    Id = h.Id,
                    Title = h.Title,
                    Address = h.Address,
                    ImageUrl = h.ImageUrl,
                    Description = h.Description,
                    PricePerMonth = h.PricePerMonth,
                    Category = (CategoryViewEnum)h.CategoryId,
                    IsRented = true
                })
                .ToListAsync();

            return Ok(houses);
        }

        /// <summary>Агент вижда създадените от него къщи</summary>
        [Authorize(Roles = "Agent")]
        [HttpGet("MyListings")]
        [Produces(typeof(IEnumerable<HouseDetailModel>))]
        public async Task<IActionResult> MyListings()
        {
            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var houses = await context.Houses
                .AsNoTracking()
                .Where(h => h.UserId == agentId)
                .Select(h => new HouseDetailModel
                {
                    Id = h.Id,
                    Title = h.Title,
                    Address = h.Address,
                    ImageUrl = h.ImageUrl,
                    Description = h.Description,
                    PricePerMonth = h.PricePerMonth,
                    Category = (CategoryViewEnum)h.CategoryId,
                    IsRented = h.RenterId != null
                })
                .ToListAsync();

            return Ok(houses);
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────

        private async Task<Category> GetOrCreateCategory(string name)
        {
            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Name == name);

            if (category == null)
            {
                category = new Category { Name = name };
                context.Categories.Add(category);
                await context.SaveChangesAsync();
            }

            return category;
        }
    }
}
