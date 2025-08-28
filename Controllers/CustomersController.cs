#nullable enable
using Microsoft.AspNetCore.Mvc;
using retailCLDVportal.Models;
using retailCLDVportal.Services;

namespace retailCLDVportal.Controllers
{
    // Optional: support both /Customers and /Customer
    [Route("Customers")]
    [Route("Customer")]
    public class CustomersController : Controller
    {
        private readonly ICustomerTableService _svc;

        public CustomersController(ICustomerTableService svc) => _svc = svc;

        // GET /Customers or /Customer
        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var items = await _svc.ListAsync(500, ct);
            // Render the non-default view name in the "Customers" folder
            return View("CustomerIndex", items); // => Views/Customers/CustomerIndex.cshtml
        }

        
        [HttpGet("Create")]
        public IActionResult Create() => View(new CustomerInput());

        // POST /Customers/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(input);

            var (ok, error, saved) = await _svc.AddAsync(input, ct);
            if (!ok || saved is null)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to save customer.");
                return View(input);
            }

            return RedirectToAction(nameof(Details), new { pk = saved.PartitionKey, rk = saved.RowKey });
        }

        // GET /Customers/Details or /Customer/Details
        [HttpGet("Details")]
        public async Task<IActionResult> Details(string pk, string rk, CancellationToken ct)
        {
            var entity = await _svc.GetAsync(pk, rk, ct);
            if (entity is null) return NotFound();
            return View(entity); // Views/Customers/Details.cshtml
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string pk, string rk, CancellationToken ct)
        {
            var (ok, error) = await _svc.DeleteAsync(pk, rk, ct);
            if (!ok) TempData["Error"] = error ?? "Delete failed.";
            else TempData["Message"] = "Customer deleted.";
            return RedirectToAction(nameof(Index));
        }
        
        // GET: /Customers/Edit?pk=...&rk=...
        [HttpGet("Edit")]
        public async Task<IActionResult> Edit(string pk, string rk, CancellationToken ct)
        {
            var entity = await _svc.GetAsync(pk, rk, ct);
            if (entity is null) return NotFound();

            return View(entity); // Views/Customers/Edit.cshtml
        }

        // POST: /Customers/Edit
        [HttpPost("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(retailCLDVportal.Models.CustomerEntity form, CancellationToken ct)
        {
            var pk = form.PartitionKey;
            var rk = form.RowKey;

            if (string.IsNullOrWhiteSpace(pk) || string.IsNullOrWhiteSpace(rk))
            {
                TempData["Error"] = "Invalid customer key.";
                return RedirectToAction(nameof(Index));
            }

            var input = new CustomerInput
            {
                FirstName = form.FirstName,
                Surname   = form.Surname,
                DateOfBirth = form.DateOfBirth,
                PhoneNumber = form.PhoneNumber,
                Email = form.Email,
                CompanyName = form.CompanyName,
                AddressLine1 = form.AddressLine1,
                AddressLine2 = form.AddressLine2,
                City = form.City,
                State = form.State,
                ZipCode = form.ZipCode,
                Country = form.Country
            };

            if (!ModelState.IsValid)
                return View(form);

            var (ok, error, updated) = await _svc.UpdateAsync(pk, rk, input, ct);
            if (!ok || updated is null)
            {
                TempData["Error"] = error ?? "Update failed.";
                return View(form);
            }

            TempData["Message"] = "Customer updated.";
            return RedirectToAction(nameof(Details), new { pk = updated.PartitionKey, rk = updated.RowKey });
        }
    }
}
