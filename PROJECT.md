# Smart Laundry — University Project (Simplified)

A minimal **ASP.NET Core 8 MVC** monolith for a small laundry business. Built for a
university coursework submission: no tests, no production hardening, just a clear
end-to-end feature that runs out of the box.

## What it does

- Customers sign in, place a pickup order, and watch its status update live.
- Staff sign in, see a queue of all orders, and click **Next** to advance an order
  through the stages.
- Both roles land on a **blank Dashboard** after login (only the top nav is shown).
- Each order gets a unique QR code (`LMS-ORD-{id}-{guid}`) rendered server-side.

## Stage pipeline

`Created → Received → Washing → Drying → Ironing → QualityCheck → ReadyForDelivery → Delivered`
(or `Cancelled` from any active stage).

When staff advances a stage the server pushes `OrderStatusChanged` over SignalR
to the customer's browser.

## Tech stack

| Concern        | Choice                                                    |
| -------------- | --------------------------------------------------------- |
| Web            | ASP.NET Core 8 MVC + Razor                                |
| Auth           | ASP.NET Core Identity (cookie) — two roles: `Customer`, `Staff` |
| Persistence    | EF Core 8.0.8 + SQLite (`laundry.db` auto-created)        |
| Real-time      | SignalR at `/hubs/orders`                                 |
| QR codes       | QRCoder 1.6.0                                             |
| Client JS      | Vanilla + `@microsoft/signalr` from CDN                   |

No CORS, no JWT, no client framework, no email/WhatsApp/PDF/file uploads.

## Folder layout (every file that matters)

```
LaundryMVC/
├── Program.cs                  Composition root — DI, middleware, routes, hub map
├── appsettings.json            Connection string + shop name only
├── LaundryMVC.csproj           Just the four NuGet refs we actually use
├── Properties/launchSettings.json   Auto-launches browser at http://localhost:5000
│
├── Models/Models.cs            AppUser, Order, OrderItem, OrderStages, forms
├── Data/AppDbContext.cs        IdentityDbContext + Orders + OrderItems + indexes
├── Data/DbSeeder.cs            Roles + default staff/admin account
│
├── Services/QrCodeService.cs   Generates (token, base64 PNG)
├── Hubs/OrderHub.cs            Single JoinGroup(string groupName) method
│
├── Controllers/
│   ├── HomeController.cs       Index (login+register), Login, Register, Logout, Dashboard
│   ├── CustomerController.cs   [Customer] Index (history) + Book
│   └── StaffController.cs      [Staff] Index (queue) + Advance + Cancel
│
├── Views/
│   ├── _ViewImports.cshtml / _ViewStart.cshtml
│   ├── Shared/_Layout.cshtml   Top nav with role-aware links + logout
│   ├── Home/Index.cshtml       Login + Register tabs (single card)
│   ├── Home/Dashboard.cshtml   Blank welcome screen (both roles)
│   ├── Customer/Index.cshtml   Booking form + order history
│   └── Staff/Index.cshtml      Queue with Next / Cancel buttons
│
└── wwwroot/
    ├── css/style.css           Single CSS file (≈200 lines)
    └── js/customer.js          Tiny SignalR bootstrap + toast
```

## Database — visible at a glance

SQLite creates `laundry.db` on first run. The schema is just **five tables**:

```
AspNetUsers         (from Identity)
   └─ FullName, Email, ...

Orders
   Id              int PK
   CustomerId      nvarchar  → AspNetUsers.Id
   CustomerName    nvarchar        ← denormalised for fast queue display
   Status          nvarchar        ← current stage
   PickupAddress   nvarchar
   PickupAt        datetime
   Notes           nvarchar
   Subtotal        decimal
   Discount        decimal
   Total           decimal
   QrToken         nvarchar UNIQUE
   QrPngBase64     nvarchar        ← base64 PNG
   CreatedAt       datetime
   UpdatedAt       datetime

OrderItems
   Id              int PK
   OrderId         int FK → Orders (cascade delete)
   ServiceName     nvarchar
   UnitPrice       decimal
   Quantity        int
```

Indexes (declared in `AppDbContext.OnModelCreating`):
`Orders.QrToken` UNIQUE · `Orders.Status` · `Orders.CustomerId`.

There is **no** Service catalogue, Coupon, Review, Delivery, Employee, or
File table — those features were intentionally dropped to keep the schema
readable for a uni submission. Coupons are honoured with a flat 10 % discount
when the booking form's `Coupon` field is filled in.

## Default account

| Email                  | Password       | Role  |
| ---------------------- | -------------- | ----- |
| `admin@laundry.local`  | `Admin@123456` | Staff |

Customers register from the home page.

## How to run

```bash
cd LaundryMVC
dotnet restore
dotnet run --launch-profile LaundryMVC
```

Then open <http://localhost:5000>. With `launchBrowser: true` in
`Properties/launchSettings.json`, `dotnet run` will open it automatically.

## Demo flow (≈2 minutes)

1. Open `/` → **Register** as a customer (e.g. `alice@example.com` / `alice123`).
2. Land on the Dashboard. Click **My Orders**.
3. Fill in the booking form: address, time, one item (`Shirt wash`, 2.50, 1),
   submit. The order appears in the history with a QR code and `Created` badge.
4. Open a second browser/incognito window → log in as
   `admin@laundry.local` / `Admin@123456`.
5. Click **Queue**. Click **Next → Received** on the order. The status badge on
   the customer's window updates instantly (SignalR).
6. Repeat to walk it through every stage to **Delivered**, or hit **Cancel**
   from any stage.

## What's deliberately *not* here

- Admin dashboard (charts, CRUD, account creation) — removed per scope.
- Email/WhatsApp/PDF invoice/file-upload services — removed per scope.
- Migrations — schema is created via `EnsureCreated()` so the project runs
  on a fresh checkout with no extra `dotnet ef` step.
- Anti-forgery, cookie auth, role-based `[Authorize]` attributes — these
  *are* here, but they're the only "security" code; there's no rate-limiting,
  no audit log, no input sanitisation beyond model binding.
- Tests — none, by design.

## Files a reader should open first

1. `Models/Models.cs` — domain in 100 lines.
2. `Data/AppDbContext.cs` — schema + indexes.
3. `Controllers/CustomerController.cs` — booking + QR minting + SignalR push.
4. `Controllers/StaffController.cs` — queue + advance.
5. `Program.cs` — composition root.