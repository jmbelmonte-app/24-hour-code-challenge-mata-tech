import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface ImportResult {
  pizzaTypes: number;
  pizzas: number;
  orders: number;
  orderItems: number;
}

export interface SalesLine {
  orderId: number;
  orderDate: string;
  orderTime: string;
  pizzaId: string;
  pizzaName: string;
  category: string;
  size: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface DashboardSummary {
  totalRevenue: number;
  totalOrders: number;
  totalPizzasSold: number;
  averageOrderValue: number;
}

export interface SalesTrendPoint {
  period: string;
  revenue: number;
  orders: number;
}

export interface TopPizza {
  pizzaId: string;
  pizzaName: string;
  category: string;
  quantity: number;
  revenue: number;
}

export interface SalesFilters {
  search: string;
  from: string;
  to: string;
  category: string;
  size: string;
  page: number;
  pageSize: number;
  sortBy: string;
  sortDirection: string;
}

@Injectable({ providedIn: 'root' })
export class PizzaSalesApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  importArchive(archive: File, replaceExisting = false): Observable<ImportResult> {
    const formData = new FormData();
    formData.append('archive', archive);
    return this.http.post<ImportResult>(`${this.apiBaseUrl}/imports/pizza-sales`, formData, {
      params: { replaceExisting },
    });
  }

  getSales(filters: SalesFilters): Observable<PagedResult<SalesLine>> {
    return this.http.get<PagedResult<SalesLine>>(`${this.apiBaseUrl}/sales`, {
      params: this.toParams(filters),
    });
  }

  getSummary(from: string, to: string): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.apiBaseUrl}/dashboard/summary`, {
      params: this.toParams({ from, to }),
    });
  }

  getSalesTrend(from: string, to: string): Observable<SalesTrendPoint[]> {
    return this.http.get<SalesTrendPoint[]>(`${this.apiBaseUrl}/dashboard/sales-trend`, {
      params: this.toParams({ from, to }),
    });
  }

  getTopPizzas(from: string, to: string, limit = 5): Observable<TopPizza[]> {
    return this.http.get<TopPizza[]>(`${this.apiBaseUrl}/dashboard/top-pizzas`, {
      params: this.toParams({ from, to, limit }),
    });
  }

  private toParams(values: object): HttpParams {
    return Object.entries(values as Record<string, string | number | boolean | undefined>).reduce((params, [key, value]) => {
      return value === undefined || value === '' ? params : params.set(key, String(value));
    }, new HttpParams());
  }
}
