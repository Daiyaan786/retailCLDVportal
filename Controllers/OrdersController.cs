#nullable enable
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using retailCLDVportal.Models;
using retailCLDVportal.Services;
//orders
namespace retailCLDVportal.Controllers
{
    [Route("Orders")]
    [Route("Order")]
    public class OrdersController : Controller
    {
        private readonly IOrderTableService _orders;
        private readonly ICustomerTableService _customers;
        private readonly IProductTableService _products;

        public OrdersController(IOrderTableService orders, ICustomerTableService customers, IProductTableService products)
        {
            _orders = orders;
            _customers = customers;
            _products = products;
        }

        // List
        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var items = await _orders.ListAsync(500, ct);
            return View(items); // Views/Orders/Index.cshtml
        }

        // ----- DETAILS -----
        [HttpGet("Details")]
        public async Task<IActionResult> Details(string pk, string rk, CancellationToken ct)
        {
            var order = await _orders.GetAsync(pk, rk, ct);
            if (order is null) return NotFound();
            return View(order); // Views/Orders/Details.cshtml
        }

        // ----- CREATE -----
        [HttpGet("Create")]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            await PopulateDropdowns(ct);
            return View(new OrderInput());
        }

        // AJAX: product info for selected product (price/stock)
        [HttpGet("ProductInfo")]
        public async Task<IActionResult> ProductInfo(string pk, string rk, CancellationToken ct)
        {
            var p = await _products.GetAsync(pk, rk, ct);
            if (p is null) return NotFound();
            return Json(new
            {
                priceCents = p.PriceCents ?? 0,
                stock      = p.StockQuantity ?? 0,
                currency   = string.IsNullOrWhiteSpace(p.Currency) ? "ZAR" : p.Currency,
                name       = p.Name
            });
        }

        // Place order
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(ct);
                return View(input);
            }

            var (ok, error, saved) = await _orders.PlaceAsync(input, ct);
            if (!ok || saved is null)
            {
                TempData["Error"] = error ?? "Failed to place order.";
                await PopulateDropdowns(ct);
                return View(input);
            }

            TempData["Message"] = $"Order {saved.OrderNo} placed.";
            return RedirectToAction(nameof(Index));
        }

        // ----- EDIT -----
        [HttpGet("Edit")]
        public async Task<IActionResult> Edit(string pk, string rk, CancellationToken ct)
        {
            var order = await _orders.GetAsync(pk, rk, ct);
            if (order is null) return NotFound();

            await PopulateDropdowns(ct);
            ViewBag.SelectedCustomerPk = order.CustomerPk;
            ViewBag.SelectedCustomerRk = order.CustomerRk;
            ViewBag.SelectedProductPk  = order.ProductPk;
            ViewBag.SelectedProductRk  = order.ProductRk;

            return View(order); // Views/Orders/Edit.cshtml (model = OrderEntity)
        }

        [HttpPost("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string pk, string rk, OrderInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                var existing = await _orders.GetAsync(pk, rk, ct);
                await PopulateDropdowns(ct);
                ViewBag.SelectedCustomerPk = input.CustomerPk;
                ViewBag.SelectedCustomerRk = input.CustomerRk;
                ViewBag.SelectedProductPk  = input.ProductPk;
                ViewBag.SelectedProductRk  = input.ProductRk;
                return View(existing ?? new OrderEntity { PartitionKey = pk, RowKey = rk });
            }

            var (ok, error, updated) = await _orders.UpdateAsync(pk, rk, input, ct);
            if (!ok || updated is null)
            {
                TempData["Error"] = error ?? "Update failed.";
                var existing = await _orders.GetAsync(pk, rk, ct);
                await PopulateDropdowns(ct);
                ViewBag.SelectedCustomerPk = input.CustomerPk;
                ViewBag.SelectedCustomerRk = input.CustomerRk;
                ViewBag.SelectedProductPk  = input.ProductPk;
                ViewBag.SelectedProductRk  = input.ProductRk;
                return View(existing ?? new OrderEntity { PartitionKey = pk, RowKey = rk });
            }

            TempData["Message"] = $"Order {updated.OrderNo} updated.";
            return RedirectToAction(nameof(Details), new { pk = updated.PartitionKey, rk = updated.RowKey });
        }

        // ----- DELETE -----
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string pk, string rk, CancellationToken ct)
        {
            var (ok, error) = await _orders.DeleteAsync(pk, rk, ct);
            if (!ok) TempData["Error"] = error ?? "Delete failed.";
            else     TempData["Message"] = "Order deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ---- helpers ----
        private async Task PopulateDropdowns(CancellationToken ct)
        {
            // Customers: table-wide (no PK filter)
            var customers = await _customers.ListAsync(500, ct);
            ViewBag.Customers = customers.Select(c => new
            {
                pk   = c.PartitionKey,
                rk   = c.RowKey,
                name = string.Join(" ", new[] { c.FirstName, c.Surname }
                                   .Where(s => !string.IsNullOrWhiteSpace(s))).Trim()
                       ?? c.CompanyName
                       ?? c.Email
                       ?? "(Unnamed customer)"
            }).ToList();

            // Products
            var products = await _products.ListAsync(500, ct);
            ViewBag.Products = products.Select(p => new
            {
                pk   = p.PartitionKey,
                rk   = p.RowKey,
                name = p.Name ?? "(Unnamed product)"
            }).ToList();
        }
    }
}
