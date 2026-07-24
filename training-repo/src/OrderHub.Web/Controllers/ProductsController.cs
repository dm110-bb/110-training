using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Services;
using OrderHub.Web.ViewModels;

namespace OrderHub.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();

        var vm = new ProductListViewModel
        {
            Products = products.Select(p => new ProductRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> LowStock([FromQuery] LowStockViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _productService.GetLowStockAsync(vm.Threshold);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(vm.Threshold), result.ErrorMessage);
            return View(vm);
        }

        vm.Products = result.Value!
            .Select(p => new LowStockRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                StockQuantity = p.StockQuantity,
                SoldQuantityLast30Days = p.SoldQuantityLast30Days
            })
            .ToList();

        return View(vm);
    }
}

