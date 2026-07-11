import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import Chart from 'chart.js/auto';
import { forkJoin } from 'rxjs';
import {
  DashboardSummary,
  ImportResult,
  PagedResult,
  PizzaSalesApiService,
  SalesFilters,
  SalesLine,
  SalesTrendPoint,
  TopPizza,
} from './pizza-sales-api.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements AfterViewInit {
  @ViewChild('trendCanvas') private trendCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('topPizzasCanvas') private topPizzasCanvas?: ElementRef<HTMLCanvasElement>;

  private readonly api = inject(PizzaSalesApiService);
  private trendChart?: Chart;
  private topPizzasChart?: Chart;

  protected readonly categories = ['Chicken', 'Classic', 'Supreme', 'Veggie'];
  protected readonly sizes = ['S', 'M', 'L', 'XL', 'XXL'];
  protected filters: SalesFilters = {
    search: '',
    from: '',
    to: '',
    category: '',
    size: '',
    page: 1,
    pageSize: 20,
    sortBy: 'orderDate',
    sortDirection: 'desc',
  };
  protected summary: DashboardSummary = { totalRevenue: 0, totalOrders: 0, totalPizzasSold: 0, averageOrderValue: 0 };
  protected sales: PagedResult<SalesLine> = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };
  protected isLoading = true;
  protected isImporting = false;
  protected errorMessage = '';
  protected importMessage = '';
  protected selectedArchive?: File;

  ngAfterViewInit(): void {
    this.refresh();
  }

  protected refresh(resetPage = false): void {
    if (resetPage) {
      this.filters.page = 1;
    }

    this.isLoading = true;
    this.errorMessage = '';
    forkJoin({
      summary: this.api.getSummary(this.filters.from, this.filters.to),
      trend: this.api.getSalesTrend(this.filters.from, this.filters.to),
      topPizzas: this.api.getTopPizzas(this.filters.from, this.filters.to),
      sales: this.api.getSales(this.filters),
    }).subscribe({
      next: ({ summary, trend, topPizzas, sales }) => {
        this.summary = summary;
        this.sales = sales;
        this.isLoading = false;
        this.renderCharts(trend, topPizzas);
      },
      error: (error: { error?: { detail?: string; title?: string } }) => {
        this.errorMessage = error.error?.detail ?? error.error?.title ?? 'The dashboard data could not be loaded.';
        this.isLoading = false;
      },
    });
  }

  protected selectArchive(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedArchive = input.files?.[0];
    this.importMessage = '';
  }

  protected importArchive(): void {
    if (!this.selectedArchive) {
      this.importMessage = 'Choose the supplied ZIP archive before importing.';
      return;
    }

    this.isImporting = true;
    this.importMessage = '';
    this.api.importArchive(this.selectedArchive).subscribe({
      next: (result) => this.handleImportSuccess(result),
      error: (error: { status: number; error?: { detail?: string; title?: string } }) => {
        const detail = error.error?.detail ?? error.error?.title ?? 'The archive could not be imported.';
        this.importMessage = error.status === 409 ? `${detail} Upload again only if you intend to replace it.` : detail;
        this.isImporting = false;
      },
    });
  }

  protected changeSort(sortBy: string): void {
    this.filters.sortDirection = this.filters.sortBy === sortBy && this.filters.sortDirection === 'desc' ? 'asc' : 'desc';
    this.filters.sortBy = sortBy;
    this.refresh();
  }

  protected changePage(page: number): void {
    if (page < 1 || page > this.sales.totalPages) {
      return;
    }

    this.filters.page = page;
    this.refresh();
  }

  protected sortIndicator(column: string): string {
    return this.filters.sortBy === column ? (this.filters.sortDirection === 'asc' ? ' ↑' : ' ↓') : '';
  }

  private handleImportSuccess(result: ImportResult): void {
    this.importMessage = `Imported ${result.orders.toLocaleString()} orders and ${result.orderItems.toLocaleString()} sales lines.`;
    this.isImporting = false;
    this.refresh(true);
  }

  private renderCharts(trend: SalesTrendPoint[], topPizzas: TopPizza[]): void {
    this.trendChart?.destroy();
    this.topPizzasChart?.destroy();

    if (this.trendCanvas) {
      this.trendChart = new Chart(this.trendCanvas.nativeElement, {
        type: 'line',
        data: {
          labels: trend.map((point) => point.period),
          datasets: [{ label: 'Revenue', data: trend.map((point) => point.revenue), borderColor: '#ff6b35', backgroundColor: 'rgba(255, 107, 53, .12)', fill: true, tension: 0.25 }],
        },
        options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { ticks: { callback: (value) => `$${value}` } } } },
      });
    }

    if (this.topPizzasCanvas) {
      this.topPizzasChart = new Chart(this.topPizzasCanvas.nativeElement, {
        type: 'bar',
        data: {
          labels: topPizzas.map((pizza) => pizza.pizzaName.replace('The ', '')),
          datasets: [{ label: 'Pizzas sold', data: topPizzas.map((pizza) => pizza.quantity), backgroundColor: ['#ff6b35', '#f7c948', '#2a9d8f', '#577590', '#9d4edd'] }],
        },
        options: { indexAxis: 'y', responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false } } },
      });
    }
  }
}
