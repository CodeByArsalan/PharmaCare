# PharmaCare 💊
### Enterprise-Grade Pharmacy Management & POS Ecosystem

PharmaCare is a comprehensive, premium-designed Point of Sale (POS) and inventory management ecosystem specifically tailored for modern pharmacies. It combines robust transaction logic with a state-of-the-art user interface to provide a seamless experience for pharmacists, administrators, and suppliers.

![PharmaCare Dashboard Preview](PharmaCare.Web/wwwroot/assets/img/dashboard_preview.png) *(Note: Replace with actual screenshot path)*

---

## ✨ Key Features

### 🚀 Modern Premium UI/UX
- **Ocean Blue & Emerald Aesthetic:** A professional, high-contrast design system featuring glassmorphism and smooth animations.
- **Responsive Layout:** A fully adaptive interface with a collapsible sidebar and mobile-optimized dashboard.
- **Dynamic Theming:** Built-in Dark and Light mode support with persistent user preferences.
- **Micro-interactions:** Interactive charts, animated stat counters, and hover effects for an enterprise-level feel.

### 📦 Supply Chain & Inventory
- **Smart Purchase Management:** Track the journey from Purchase Orders to Goods Received Notes (GRN).
- **Inventory Control:** Real-time stock tracking with low-stock alerts and expiration monitoring.
- **Purchase Returns:** Automated return workflows with accurate cost-price preservation.

### 💰 Finance & Ledger
- **Supplier Payments:** Comprehensive payment tracking supporting advance payments, partial settlements, and full payouts.
- **Consolidated Ledger:** Automatically synchronized financial accounts for all parties.
- **Transaction Recalculation:** Intelligent balance recalculation logic to ensure data integrity across all orders.

### 📊 Intelligence & Reporting
- **Interactive Dashboard:** 7-day sales trends visualized via Chart.js with dynamic hover tooltips.
- **Role-Based Access:** Secure modules for Administration, Configuration, and Transaction management.

---

## 🛠 Technology Stack

- **Backend:** ASP.NET Core 8+ (MVC & Web API)
- **Architecture:** Clean Architecture / N-Tier (Domain, Infrastructure, Application layers)
- **Persistence:** Entity Framework Core with SQL Server
- **Frontend:** 
    - Bootstrap 5 & Vanilla CSS
    - JavaScript (ES6+)
    - Chart.js (Data Visualization)
    - SweetAlert2 (Interactive Dialogs)
    - DataTables (Advanced Grid Management)

---

## 🏗 Architecture Overview

PharmaCare follows a decoupled **Clean Architecture** pattern to ensure scalability and maintainability:

- **PharmaCare.Domain:** Enterprise entities, value objects, and repository interfaces.
- **PharmaCare.Application:** Business logic, DTOs, and Service implementations.
- **PharmaCare.Infrastructure:** Data access layer, EF Core patterns, and external services.
- **PharmaCare.Web:** The user-facing web interface and API gateways.

---

## 🛠 Installation & Setup

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server/)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)

### Getting Started
1. **Clone the repository:**
   ```bash
   git clone https://github.com/Arsalan/PharmaCare.git
   ```
2. **Configure Database:**
   Update the `ConnectionStrings` in `PharmaCare.Web/appsettings.json`.
3. **Apply Migrations:**
   ```bash
   dotnet ef database update --project PharmaCare.Infrastructure --startup-project PharmaCare.Web
   ```
4. **Run the application:**
   ```bash
   dotnet run --project PharmaCare.Web
   ```

---

## 📸 Screenshots & Showcase

| Dashboard | Purchase Management | POS Screen |
| :--- | :--- | :--- |
| ![Dashboard](PharmaCare.Web/wwwroot/assets/img/ss_dashboard.png) | ![Purchase](PharmaCare.Web/wwwroot/assets/img/ss_purchase.png) | ![POS](PharmaCare.Web/wwwroot/assets/img/ss_pos.png) |

---

## 🤝 Contributing
Contributions are welcome! Please read the `CONTRIBUTING.md` for details on our code of conduct and the process for submitting pull requests.

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
