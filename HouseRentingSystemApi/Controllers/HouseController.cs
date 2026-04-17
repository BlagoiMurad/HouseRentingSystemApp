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
    [Route("api/[controller]")]
    public class HouseController : ControllerBase
    {
        private AppDbContext context;

        public HouseController(AppDbContext context)
        {
            this.context = context;
        }

        [HttpGet("All")]
        [Produces(typeof(IEnumerable<HouseDetailModel>))]
        public async Task<IActionResult> GetAll()
        {
            var model = await context.Houses
                .AsNoTracking()
                .Select(h => new HouseDetailModel()
                {
                    Id = h.Id,
                    Title = h.Title,
                    Address = h.Address,
                    ImageUrl = h.ImageUrl,
                    Description = h.Description,
                    PricePerMonth = h.PricePerMonth,
                    Category = (CategoryViewEnum)h.CategoryId
                })
                .ToListAsync();

            return Ok(model);
        }


        [HttpGet("{id}")]
        [Produces(typeof(HouseDetailModel))]
        public async Task<IActionResult> GetById(int id)
        {
            var house = await context.Houses
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == id);

            if (house == null)
            {
                return NotFound();
            }

            return Ok(new HouseDetailModel()
            {
                Id = house.Id,
                Title = house.Title,
                Address = house.Address,
                ImageUrl = house.ImageUrl,
                Description = house.Description,
                PricePerMonth = house.PricePerMonth,
                Category = (CategoryViewEnum)house.CategoryId
            });
        }

        //implementirame end point edit. da polu`awame danni json za kushat s post zaqwka kym edit aktiona i da editwame ku]a kym bazata
        [Authorize]
        [HttpPost]
        [Produces(typeof(HouseDetailModel))]
        public async Task<IActionResult> Create([FromBody] HouseDetailModel model)
        {
            if (ModelState.IsValid == false)
            {
                return BadRequest();
            }

            var newHouse = new House()
            {
                Description = model.Description,
                PricePerMonth = model.PricePerMonth,
                Address = model.Address,
                Title = model.Title,
                ImageUrl = model.ImageUrl
            };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);  

            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Name ==  model.Category
                .ToString());
            if(category == null)
            {
                var newCategory = new Category()
                {
                    Name = model.Category.ToString(),
                };
                context.Categories.Add(newCategory);
                await context.SaveChangesAsync();
                newHouse.CategoryId = newCategory.Id; 
                
            }
            else
            {
                newHouse.CategoryId = category.Id;
            }
            context.Houses.Add(newHouse);
            await context.SaveChangesAsync();
            return Created($"api/{newHouse.Id}", new HouseDetailModel()
                {
                    Address = newHouse.Address,
                    ImageUrl = newHouse.ImageUrl,
                    Title = newHouse.Title,
                    Description = newHouse.Description,
                    PricePerMonth = newHouse.PricePerMonth,
                    Category = model.Category
            });

        }
        [Authorize]
        [HttpPost("Edit/{id}")]
        [Produces(typeof(HouseDetailModel))]
        public async Task<IActionResult> Edit(int id, [FromBody] HouseDetailModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var house = await context.Houses
                .FirstOrDefaultAsync(h => h.Id == id);

            if (house == null)
            {
                return NotFound("The house is already deleted or It is not been find");
            }

            house.Title = model.Title;
            house.Address = model.Address;
            house.ImageUrl = model.ImageUrl;
            house.Description = model.Description;
            house.PricePerMonth = model.PricePerMonth;

            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Name == model.Category.ToString());

            if (category == null)
            {
                category = new Category()
                {
                    Name = model.Category.ToString()
                };

                context.Categories.Add(category);
                await context.SaveChangesAsync();
            }

            house.CategoryId = category.Id;

            await context.SaveChangesAsync();

            return Ok(new HouseDetailModel()
            {
                Id= house.Id,
                Title = house.Title,
                Address = house.Address,
                ImageUrl = house.ImageUrl,
                Description = house.Description,
                PricePerMonth = house.PricePerMonth,
                Category = model.Category
            });
        }

        [Authorize]
        [HttpPost("Delete/{id}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            var house = await context.Houses
                .FirstOrDefaultAsync(h => h.Id == id);

            if (house == null)
            {
                return NotFound("The house is already deleted or It is not been find");
            }

            context.Houses.Remove(house);
            await context.SaveChangesAsync();

            return Ok(new { message = $"House with id {id} was deleted successfully." });
        }
        
    }
}