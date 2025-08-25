#nullable enable
using Microsoft.AspNetCore.Mvc;
using retailCLDVportal.Models;
using retailCLDVportal.Services;

namespace retailCLDVportal.Controllers
{
    [Route("Products")]
    [Route("Product")]
    public class ProductsController : Controller
    {
        private readonly IProductTableService _svc;
        public ProductsController(IProductTableService svc) => _svc = svc;

        // List
        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var items = await _svc.ListAsync(500, ct);
            return View(items); // Views/Products/Index.cshtml
        }

        // Details
        [HttpGet("Details")]
        public async Task<IActionResult> Details(string pk, string rk, CancellationToken ct)
        {
            var p = await _svc.GetAsync(pk, rk, ct);
            if (p is null) return NotFound();
            return View(p); // Views/Products/Details.cshtml
        }

        // Create (GET)
        [HttpGet("Create")]
        public IActionResult Create() => View(new ProductInput());

        // Create (POST)
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(input);
            var (ok, error, saved) = await _svc.AddAsync(input, ct);
            if (!ok || saved is null)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to save product.");
                return View(input);
            }
            TempData["Message"] = "Product created.";
            return RedirectToAction(nameof(Index));
        }

        // Edit (GET)
        [HttpGet("Edit")]
        public async Task<IActionResult> Edit(string pk, string rk, CancellationToken ct)
        {
            var p = await _svc.GetAsync(pk, rk, ct);
            if (p is null) return NotFound();
            return View(p); // Views/Products/Edit.cshtml (optional later)
        }

        // Edit (POST)
        [HttpPost("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string pk, string rk, ProductInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                // For a simple edit page that binds to ProductEntity, you can map back if needed
                TempData["Error"] = "Invalid input.";
                return RedirectToAction(nameof(Index));
            }

            var (ok, error, updated) = await _svc.UpdateAsync(pk, rk, input, ct);
            if (!ok || updated is null)
            {
                TempData["Error"] = error ?? "Update failed.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Message"] = "Product updated.";
            return RedirectToAction(nameof(Details), new { pk = updated.PartitionKey, rk = updated.RowKey });
        }

        // Delete
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string pk, string rk, CancellationToken ct)
        {
            var (ok, error) = await _svc.DeleteAsync(pk, rk, ct);
            if (!ok) TempData["Error"] = error ?? "Delete failed.";
            else     TempData["Message"] = "Product deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
