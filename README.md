```markdown
# Pizza Sales Dashboard

A full-stack Pizza Place sales dashboard built with .NET, EF Core, SQLite, and Angular.

## Technology Stack

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQLite
- Angular 20
- Chart.js
- xUnit
- GitHub Actions

## Features

- Import pizza sales data from a ZIP archive.
- Validate CSV files, headers, data types, duplicates, and relationships.
- Store data in a normalized SQLite schema.
- Search and filter sales by:
  - Text
  - Date range
  - Category
  - Pizza size
- Sort and paginate sales records.
- Display dashboard metrics:
  - Total revenue
  - Total orders
  - Pizzas sold
  - Average order value
- Display monthly revenue trends.
- Display top-selling pizzas.
- Swagger/OpenAPI documentation.
- Health-check endpoint.
- Automated backend and frontend tests.

## Project Structure

```text
src/
├── PizzaSales.Domain/
├── PizzaSales.Application/
├── PizzaSales.Infrastructure/
└── PizzaSales.Api/

frontend/
└── Angular dashboard

tests/
└── PizzaSales.Tests/

data/
└── pizza_place_sales_archive.zip
```

## Running the API

Prerequisites:

- .NET SDK 10
- Node.js 24
- pnpm 11+

From the repository root:

```bash
dotnet restore PizzaSales.sln
dotnet run --project src/PizzaSales.Api
```

The API runs at:

```text
http://localhost:5186
```

Swagger is available at:

```text
http://localhost:5186/swagger
```

The SQLite database is created under:

```text
App_Data/pizza-sales.db
```

## Running the Angular Application

```bash
cd frontend
pnpm install
pnpm start
```

Open:

```text
http://localhost:4200
```

The frontend uses the API URL configured in:

```text
frontend/src/environments/environment.ts
```

## Importing the Dataset

Upload:

```text
data/pizza_place_sales_archive.zip
```

through the dashboard import control.

The archive contains:

```text
orders.csv
order_details.csv
pizzas.csv
pizza_types.csv
```

The import endpoint can also be called directly:

```bash
curl -X POST \
  -F "archive=@data/pizza_place_sales_archive.zip" \
  http://localhost:5186/api/imports/pizza-sales
```

To explicitly replace an existing import:

```bash
curl -X POST \
  -F "archive=@data/pizza_place_sales_archive.zip" \
  "http://localhost:5186/api/imports/pizza-sales?replaceExisting=true"
```

## API Endpoints

| Endpoint | Description |
| --- | --- |
| `POST /api/imports/pizza-sales` | Import the sales archive |
| `GET /api/sales` | Search, filter, sort, and paginate sales |
| `GET /api/dashboard/summary` | Return dashboard KPI metrics |
| `GET /api/dashboard/sales-trend` | Return monthly revenue trends |
| `GET /api/dashboard/top-pizzas` | Return top-selling pizzas |
| `GET /health` | API health check |

Example sales query:

```text
GET /api/sales?search=California&category=Chicken&page=1&pageSize=20&sortBy=lineTotal&sortDirection=desc
```

## Data Model

The database contains four normalized tables:

- `PizzaTypes`
- `Pizzas`
- `Orders`
- `OrderItems`

`OrderItems` stores the unit price at the time of sale as integer cents. This prevents floating-point currency errors and preserves historical revenue if product prices change later.

## Testing

Run backend tests:

```bash
dotnet test PizzaSales.sln
```

Build the Angular application:

```bash
cd frontend
pnpm build
```

Run Angular tests:

```bash
pnpm exec ng test --watch=false --browsers=ChromeHeadless
```

The supplied dataset contains:

- 32 pizza types
- 96 pizzas
- 21,350 orders
- 48,620 order lines
- 49,574 pizzas sold

## Source Dataset

The dataset is sourced from the public Kaggle Pizza Place Sales dataset:

https://www.kaggle.com/datasets/mysarahmadbhat/pizza-place-sales
```