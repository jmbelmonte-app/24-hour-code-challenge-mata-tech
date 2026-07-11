import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PizzaSalesApiService } from './pizza-sales-api.service';

describe('PizzaSalesApiService', () => {
  let service: PizzaSalesApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PizzaSalesApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('maps dashboard filters to query parameters', () => {
    service.getSales({ search: 'hawaiian', from: '2015-01-01', to: '', category: 'Classic', size: 'M', page: 2, pageSize: 20, sortBy: 'lineTotal', sortDirection: 'asc' }).subscribe();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/api/sales'));
    expect(request.request.params.get('search')).toBe('hawaiian');
    expect(request.request.params.get('from')).toBe('2015-01-01');
    expect(request.request.params.has('to')).toBeFalse();
    expect(request.request.params.get('sortBy')).toBe('lineTotal');
    request.flush({ items: [], page: 2, pageSize: 20, totalCount: 0, totalPages: 0 });
  });
});
