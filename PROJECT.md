# Smart Laundry Management

ASP.NET Core 8 MVC application for laundry pickup orders, staff processing, service catalog management, and administration.

## Features

- Customers register, choose active catalog services, submit pickup orders, and track order status with QR codes and SignalR updates.
- Staff process the shared order queue, advance order stages, and cancel orders.
- Admins view revenue and order-status analytics, manage customer/staff access, create staff accounts, manage the service catalog, and export CSV data.

## Roles and pages

| Role | Access |
| --- | --- |
| Customer | `/Customer` for booking and order history |
| Staff | `/Staff` for the order queue |
| Admin | `/Admin` for analytics and management; also `/Staff` for the queue |

New public registrations receive the Customer role. Staff accounts can be created by an Admin. Admin accounts cannot be created through public registration.

## Run locally

Requirements: .NET SDK 8 or a compatible newer SDK.

```powershell
dotnet restore
dotnet build
dotnet run --urls http://localhost:5000
```

Open <http://localhost:5000>. The SQLite database is created as `laundry.db` in the application working directory.

## Development accounts

The seeder creates these accounts when they do not already exist:

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@laundry.local` | `Admin@123456` |
| Staff | `staff@laundry.local` | `Staff@123456` |

These are development credentials defined in `Data/DbSeeder.cs`. Change them before deploying the application. New customer accounts are created from the registration tab.

## Main components

- `Program.cs`: dependency injection, Identity, middleware, MVC routes, and SignalR hub.
- `Models/Models.cs`: users, orders, catalog, form models, and analytics view models.
- `Data/AppDbContext.cs`: Identity, orders, order items, and service catalog.
- `Data/DbSeeder.cs`: roles, development accounts, default services, and non-destructive service-table setup.
- `Controllers/AdminController.cs`: admin analytics, account management, service catalog, and CSV downloads.
- `Controllers/CustomerController.cs`: customer order history and catalog-priced booking.
- `Controllers/StaffController.cs`: order queue and status transitions.
- `Services/QrCodeService.cs`: QR token and PNG generation.
- `Hubs/OrderHub.cs`: SignalR groups for order status updates.

## Database behavior

EF Core `EnsureCreated` creates the Identity and order schema for a new database. Startup separately creates the `ServiceCatalog` table if it is missing, preserving data in an existing database. Default services are inserted only when the catalog is empty. The application does not use EF migrations.

Each order stores a snapshot of service name and price at booking time. Editing or deactivating a catalog service does not rewrite existing orders. Revenue analytics sum non-cancelled order totals by creation month; the application does not currently track payments or collected revenue.

## Security and deployment notes

- Admin, Staff, and Customer controllers enforce role authorization; form posts use anti-forgery tokens.
- Admin can change Customer/Staff roles and deactivate/reactivate non-admin accounts. Admin accounts are protected from these user-management actions.
- Seed passwords are hard-coded for local development. Production deployments should replace the seeding credentials and configure secrets, HTTPS, logging, and operational protections.
- No payment processing, password-recovery email, audit log, or automated test project is included.