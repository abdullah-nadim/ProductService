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
            try
            {
                _logger.LogInformation("Getting all products");
                var products = await _productServices.GetAllProducts();
                return Ok(ProductContract.ToContracts(products, _mapper));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all products");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetAllActive()
        {
            try
            {
                _logger.LogInformation("Getting all active products");
                var products = await _productServices.GetAllActiveProducts();
                return Ok(ProductContract.ToContracts(products, _mapper));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active products");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            if (page < 1 || size < 1 || size > 100)
                return BadRequest("page must be >= 1 and size must be between 1 and 100.");

            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged products");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById([FromRoute] long id)
        {
            try
            {
                _logger.LogInformation("Getting product with ID: {Id}", id);
                var product = await _productServices.GetProductById(id);

                if (product == null)
                {
                    _logger.LogWarning("Product with ID {Id} not found", id);
                    return NotFound($"Product with ID {id} not found");
                }

                return Ok(ProductContract.ToContract(product, _mapper));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductContract contract)
        {
            try
            {
                _logger.LogInformation("Creating product: {Name}", contract.Name);
                var id = await _productServices.CreateProduct(contract.ToModel(_mapper));
                _logger.LogInformation("Product created with ID: {Id}", id);
                return Ok(new { id, message = "Product created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] ProductContract contract)
        {
            try
            {
                _logger.LogInformation("Updating product with ID: {Id}", id);
                contract.Id = id;
                await _productServices.UpdateProduct(contract.ToModel(_mapper));
                _logger.LogInformation("Product {Id} updated successfully", id);
                return Ok(new { message = "Product updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete([FromRoute] long id)
        {
            try
            {
                _logger.LogInformation("Deleting product with ID: {Id}", id);
                var product = await _productServices.GetProductById(id);

                if (product == null)
                {
                    _logger.LogWarning("Product with ID {Id} not found", id);
                    return NotFound($"Product with ID {id} not found");
                }

                await _productServices.DeleteProduct(id);
                _logger.LogInformation("Product {Id} deleted successfully", id);
                return Ok(new { message = "Product deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
