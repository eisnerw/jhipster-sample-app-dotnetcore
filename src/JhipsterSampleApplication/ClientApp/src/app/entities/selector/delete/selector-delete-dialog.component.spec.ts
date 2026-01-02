jest.mock('@ng-bootstrap/ng-bootstrap');

import { ComponentFixture, TestBed, inject, fakeAsync, tick } from '@angular/core/testing';
import { HttpResponse } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

import { SelectorService } from '../service/selector.service';

import { SelectorDeleteDialogComponent } from './selector-delete-dialog.component';

describe('Component Tests', () => {
  describe('Selector Management Delete Component', () => {
    let comp: SelectorDeleteDialogComponent;
    let fixture: ComponentFixture<SelectorDeleteDialogComponent>;
    let service: SelectorService;
    let mockActiveModal: NgbActiveModal;

    beforeEach(async () => {
      const activeModalMock = { close: jest.fn(), dismiss: jest.fn() } as unknown as NgbActiveModal;

      await TestBed.configureTestingModule({
        imports: [SelectorDeleteDialogComponent],
        providers: [provideHttpClient(), provideHttpClientTesting(), { provide: NgbActiveModal, useValue: activeModalMock }],
      })
        .overrideTemplate(SelectorDeleteDialogComponent, '')
        .compileComponents();
      fixture = TestBed.createComponent(SelectorDeleteDialogComponent);
      comp = fixture.componentInstance;
      service = TestBed.inject(SelectorService);
      mockActiveModal = TestBed.inject(NgbActiveModal);
    });

    beforeEach(() => {
      fixture.detectChanges();
    });

    describe('confirmDelete', () => {
      it('Should call delete service on confirmDelete', inject(
        [],
        fakeAsync(() => {
          // GIVEN
          jest.spyOn(service, 'delete').mockReturnValue(of(new HttpResponse<{}>({ body: {} })));

          // WHEN
          comp.confirmDelete(123);
          tick();

          // THEN
          expect(service.delete).toHaveBeenCalledWith(123);
          expect(mockActiveModal.close).toHaveBeenCalledWith('deleted');
        }),
      ));

      it('Should not call delete service on clear', () => {
        // GIVEN
        jest.spyOn(service, 'delete');

        // WHEN
        comp.cancel();

        // THEN
        expect(service.delete).not.toHaveBeenCalled();
        expect(mockActiveModal.close).not.toHaveBeenCalled();
        expect(mockActiveModal.dismiss).toHaveBeenCalled();
      });
    });
  });
});
