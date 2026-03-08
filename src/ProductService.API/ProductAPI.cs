using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProductService.Contracts;
using ProductService.Models;

namespace ProductService.API
{
    [Route("api/v1/products")]
    [ApiController]
    public class ProductAPI : ControllerBase
    {
        private readonly IProductServices _productServices;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductAPI> _logger;

        public ProductAPI(IProductServices productServices, IMapper mapper, ILogger<ProductAPI> logger)
        {
            _productServices = productServices;
            _mapper = mapper;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            _logger.LogInformation("Getting all products");
            var products = await _productServices.GetAllProducts();
            return Ok(ProductContract.ToContracts(products, _mapper));
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetAllActive()
        {
            _logger.LogInformation("Getting all active products");
            var products = await _productServices.GetAllActiveProducts();
            return Ok(ProductContract.ToContracts(products, _mapper));
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            if (page < 1 || size < 1 || size > 100)
                return BadRequest("page must be >= 1 and size must be between 1 and 100.");

            _logger.LogInformation("Getting paged products: page={Page}, size={Size}", page, size);
            var result = await _productServices.GetPagedProductsAsync(page, size, HttpContext.RequestAborted);

            return Ok(new
            {
                data = ProductContract.ToContracts(result.Items, _mapper),
                page = result.PageNumber,
                size = result.PageSize,
                total = result.TotalItems,
                totalPages = result.TotalPages
            });
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById([FromRoute] long id)
        {
            _logger.LogInformation("Getting product {Id}", id);
            var product = await _productServices.GetProductById(id);

            if (product == null)
            {
                _logger.LogWarning("Product {Id} not found", id);
                return NotFound($"Product with ID {id} not found");
            }

            return Ok(ProductContract.ToContract(product, _mapper));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductContract contract)
        {
            _logger.LogInformation("Creating product: {Name}", contract.Name);
            var id = await _productServices.CreateProduct(contract.ToModel(_mapper));
            _logger.LogInformation("Product created with ID: {Id}", id);
            return Ok(new { id, message = "Product created successfully" });
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] ProductContract contract)
        {
            _logger.LogInformation("Updating product {Id}", id);
            contract.Id = id;
            await _productServices.UpdateProduct(contract.ToModel(_mapper));
            return Ok(new { message = "Product updated successfully" });
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete([FromRoute] long id)
        {
            _logger.LogInformation("Deleting product {Id}", id);
            var product = await _productServices.GetProductById(id);

            if (product == null)
            {
                _logger.LogWarning("Product {Id} not found", id);
                return NotFound($"Product with ID {id} not found");
            }

            await _productServices.DeleteProduct(id);
            return Ok(new { message = "Product deleted successfully" });
        }
    }
}
