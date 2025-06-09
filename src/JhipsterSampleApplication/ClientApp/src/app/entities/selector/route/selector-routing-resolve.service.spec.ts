import { TestBed, inject } from '@angular/core/testing';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import SelectorRoutingResolveService from './selector-routing-resolve.service';

describe('Selector routing resolve service', () => {
  let service: SelectorRoutingResolveService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SelectorRoutingResolveService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
  });

  beforeEach(inject(
    [SelectorRoutingResolveService],
    (injectedService: SelectorRoutingResolveService) => {
      service = injectedService;
    },
  ));

  describe('resolve', () => {
    it('should return Observable<Selector>', () => {
      service.resolve().subscribe((result: unknown) => {
        expect(result).toBeTruthy();
      });
    });
  });
});
