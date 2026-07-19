# MTE Stock — نظام إدارة المخزون والمبيعات

**MTE Stock** is a full-featured desktop inventory management and point-of-sale (POS) system for small to medium businesses. Built with **WPF (.NET 9)** and **SQLite**, fully localized in **Arabic** with RTL support.

---

## Features

### 📦 Products & Multi-Level Units
- Define products with a **hierarchical unit system**: Carton → Box → Piece
- Each unit has its own retail/wholesale price and barcode
- Automatic price calculation: box price = piece price × pieces per box; carton price = box price × boxes per carton

### 📊 Inventory Management (FIFO)
- **Batch-based** inventory tracking with **FIFO** cost calculation
- Stock in / stock out / adjustments / returns / shortages
- Automatic conversion between unit levels (e.g., 2 cartons, 5 boxes, 3 pieces)
- Real-time stock sufficiency checking before orders

### 🧾 Invoicing & Orders
- Create invoices with multiple orders per invoice
- Support for **retail** and **wholesale** pricing per order item
- Track invoice status: Open / Partially Paid / Paid / Cancelled
- Order editing with automatic stock return and re-deduction

### 👥 Customer Management
- Customer records with phone, address, notes
- Customer status indicators: Good / Has Unpaid / Overdue
- View customer invoice history and payment summary

### 💳 Payments & Discounts
- Record, edit, and confirm payments
- Apply discounts to invoices
- Merge invoices

### 🖨️ Printing & PDF Export
- HTML receipt preview with QR code and barcode
- Direct thermal/printer support via WPF printing
- **PDF export** using headless Edge/Chrome

### 🔄 Backup & Restore
- Automatic backups on startup, on operation, and periodic timer
- Manual backup/restore from Settings
- Keeps up to 5 latest backups with auto-cleanup

### 📈 Reports & Analytics
- Dashboard with key metrics: today's sales, costs, profit, customer count
- Sales/cost/profit reports
- Stock movement history

---

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# 12 (.NET 9.0) |
| UI | Windows Presentation Foundation (WPF) |
| Database | SQLite via Entity Framework Core 9.0 |
| PDF Export | Headless Edge/Chrome (`--print-to-pdf`) |
| Barcodes | ZXing.Net (CODE_128) |
| QR Codes | QRCoder |
| Installer | Inno Setup 6 |

---

## Project Structure

```
├── App.xaml / App.xaml.cs          # Application entry, startup, global error handling
├── Converters/                     # Value converters and attached behaviors
├── Data/
│   ├── AppDbContext.cs             # EF Core DbContext (8 DbSets)
│   └── DbSeeder.cs                 # Sample data seeder
├── Models/                         # Entity classes (Product, Invoice, Order, etc.)
├── Services/                       # Business logic layer
│   ├── AppConfig.cs                # JSON configuration management
│   ├── BackupService.cs            # Database backup/restore
│   ├── BillPrintService.cs         # HTML receipt generation
│   ├── InventoryService.cs         # FIFO stock calculations
│   ├── NotificationManager.cs      # Toast notification system
│   ├── PdfExportService.cs         # HTML → PDF conversion
│   └── ReceiptPrinter.cs           # Thermal printer support
├── Styles/                         # Theme and control styles
├── Views/                          # All pages and dialogs (29 controls)
│   ├── Pages: Dashboard, Products, Customers, Invoices, Reports, Settings
│   └── Dialogs: Product, Customer, Order, Invoice, Stock, Print, etc.
├── installer/                      # Inno Setup installer script
└── publish/                        # Published builds
```

---

## Database

- **Engine:** SQLite
- **Location:** `%LOCALAPPDATA%\MTE Stock\inventory.db`
- **Tables:** Products, ProductUnits, Customers, Invoices, Orders, OrderItems, Payments, InventoryBatches, InventoryMovements
- **Auto-created** on first run via `EnsureCreated()`

---

## Setup & Installation

### Prerequisites
- Windows 10/11 (64-bit)
- [.NET 9.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (if not using self-contained build)

### Build from Source
```powershell
# Restore & build
dotnet restore
dotnet build -c Release

# Publish self-contained
dotnet publish -c Release -r win-x64 --self-contained true -o publish

# Build installer (requires Inno Setup 6)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\setup.iss
```

### Install
Run the generated `installer\MTEStock_Setup.exe` and enter the installation password.

**Default password:** Contact the developer

---

## Configuration

Settings are stored in `%LOCALAPPDATA%\MTE Stock\config.json`:
- Location name, address, phone
- Backup folder and auto-backup interval
- Printer selection
- Print display settings
- Password (SHA256 hash)

---

## Error Logging

Errors are logged to two locations:
- `%LOCALAPPDATA%\MTE Stock\error.log`
- `error.log` in the application directory

---

## Developer

**Eng. Mustafa Talaat**  
📞 01116626164  
🔗 [https://github.com/anomalyco](https://github.com/anomalyco)

---

## License

Proprietary — All rights reserved.
