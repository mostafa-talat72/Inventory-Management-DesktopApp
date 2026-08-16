# MTE Stock — Inventory & Sales Management System

A desktop WPF application (Arabic) for comprehensive inventory and sales management with multi-level unit support (Carton > Box > Piece), dual pricing (Retail / Wholesale), and FIFO costing.

---

## Features

- **Product Management**: Add, edit, delete products with images, barcodes, and QR codes
- **Hierarchical Categories**: Organize products into categories and sub-categories (unlimited levels)
- **Multi-Level Units**: Each product supports 3 units (Carton ← Box ← Piece) with separate Retail/Wholesale pricing
- **Stock Management**: Stock-in with cost recording, automatic FIFO deduction
- **Sales (Invoices)**: Create invoices with multiple orders, quantity editing, automatic stock deduction
- **Stock Movement Log**: Every movement recorded (Stock-In, Stock-Out, Return, Adjustment) with unit price, total, and reference
- **Customers**: Customer data management, invoice history, and balance tracking
- **Payments**: Payment recording with edit and delete support
- **Invoice Merging**: Merge multiple invoices into one
- **Reports**: Sales and profit reports (daily, monthly, yearly, by customer)
- **Printing**: Invoice printing with thermal and network printer support
- **PDF Export**: Export reports and invoices to PDF
- **Backup**: Automatic encrypted backup with flexible settings
- **Security**: Password login (PBKDF2), amount visibility toggle
- **Themes**: Light and Dark mode UI
- **Search & Filter**: Smart search with category filtering

---

## Technologies

- **.NET 9** — Windows Presentation Foundation (WPF)
- **Entity Framework Core 9** — SQLite
- **QRCoder** — QR code generation
- **ZXing.Net** — Barcode reading/writing
- **Inno Setup** — Application installer

---

## Project Structure

```
MTE Stock/
├── App.xaml                # Application entry point
├── MainWindow.xaml         # Main window
├── Models/                 # Data models
│   ├── Category.cs         # Product category (hierarchical)
│   ├── Product.cs          # Product
│   ├── ProductUnit.cs      # Unit (Carton/Box/Piece)
│   ├── Customer.cs         # Customer
│   ├── Invoice.cs          # Invoice
│   ├── Order.cs            # Order
│   ├── OrderItem.cs        # Order item
│   ├── Payment.cs          # Payment
│   ├── InventoryBatch.cs   # Stock batch (FIFO)
│   └── InventoryMovement.cs# Stock movement
├── Views/                  # User interfaces
│   ├── ProductsPage        # Products page
│   ├── StockInDialog       # Stock-in dialog
│   ├── AddOrderDialog      # Add order dialog
│   ├── InvoicesPage        # Invoices page
│   ├── CustomersPage       # Customers page
│   ├── DashboardPage       # Dashboard
│   ├── ReportsPage         # Reports
│   ├── SettingsPage        # Settings
│   └── ...                 # Other dialogs
├── Services/               # Service layer
│   ├── InventoryService    # Inventory logic & FIFO
│   ├── BillPrintService    # Invoice printing
│   ├── BackupService       # Backup management
│   ├── ThemeService        # Theme management
│   └── ...
├── Data/                   # Database layer
│   ├── AppDbContext.cs     # EF Core DbContext
│   └── DbSeeder.cs        # Database seeder
├── Styles/                 # Theme files
├── Converters/             # XAML value converters
└── installer/              # Inno Setup installer script
```

---

## Requirements

- **OS**: Windows 10 / 11
- **Framework**: .NET 9.0 Runtime
- **Database**: SQLite (embedded, no installation required)
- **Installer**: `MTEStock_Setup.exe`

Database and config files are stored in `%LOCALAPPDATA%\MTE Stock\`

---

## Installation

1. Run `MTEStock_Setup`
2. The installer is password-protected — contact the developer for the password
3. Follow the installation steps (default path: `%PROGRAMFILES%\MTE Stock`)
4. Desktop and Start Menu shortcuts will be created
5. Default login password: `123456`

---

## Developer

**Eng. Mostafa Talat** — Software Solutions  
Phone: `01116626164`  
Email: `mostafa.taal3t@gmail.com`

© 2025 Eng. Mostafa Talat for Software Solutions
