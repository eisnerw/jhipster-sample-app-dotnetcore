import { TestBed } from '@angular/core/testing';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of } from 'rxjs';

import selectorResolve from './selector-routing-resolve.service';
import { ISelector } from '../selector.model';
import { SelectorService } from '../service/selector.service';

describe('Selector routing resolve service', () => {
  let mockRouter: Router;
  let service: SelectorService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: Router,
          useValue: { navigate: jest.fn().mockResolvedValue(true) },
        },
      ],
    });

    mockRouter = TestBed.inject(Router);
    service = TestBed.inject(SelectorService);
  });

  describe('resolve', () => {
    it('should return selector when id is provided', done => {
      const route = { params: { id: 123 } } as any;
      const selector: ISelector = { id: 123 };
      jest.spyOn(service, 'find').mockReturnValue(of(new HttpResponse({ body: selector })));

      TestBed.runInInjectionContext(() => {
        selectorResolve(route).subscribe(result => {
          expect(service.find).toHaveBeenCalledWith(123);
          expect(result).toEqual(selector);
          done();
        });
      });
    });

    it('should return null when id is not provided', done => {
      const route = { params: {} } as any;
      jest.spyOn(service, 'find');

      TestBed.runInInjectionContext(() => {
        selectorResolve(route).subscribe(result => {
          expect(service.find).not.toHaveBeenCalled();
          expect(result).toBeNull();
          done();
        });
      });
    });

    it('should navigate to 404 page if data not found on server', done => {
      const route = { params: { id: 123 } } as any;
      jest
        .spyOn(service, 'find')
        .mockReturnValue(of(new HttpResponse<ISelector>({ body: null as unknown as ISelector })));

      TestBed.runInInjectionContext(() => {
        selectorResolve(route).subscribe({
          complete: () => {
            expect(service.find).toHaveBeenCalledWith(123);
            expect(mockRouter.navigate).toHaveBeenCalledWith(['404']);
            done();
          },
        });
      });
    });
  });
});
