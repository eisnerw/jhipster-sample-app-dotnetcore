import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpResponse } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';

import { JhipsterTestModule } from '../../../test.module';
import { SelectorUpdateComponent } from 'app/entities/selector/selector-update.component';
import { SelectorService } from 'app/entities/selector/selector.service';
import { Selector } from 'app/shared/model/selector.model';

describe('Component Tests', () => {
  describe('Selector Management Update Component', () => {
    let comp: SelectorUpdateComponent;
    let fixture: ComponentFixture<SelectorUpdateComponent>;
    let service: SelectorService;

    beforeEach(() => {
      TestBed.configureTestingModule({
        imports: [JhipsterTestModule],
        declarations: [SelectorUpdateComponent],
        providers: [FormBuilder],
      })
        .overrideTemplate(SelectorUpdateComponent, '')
        .compileComponents();

      fixture = TestBed.createComponent(SelectorUpdateComponent);
      comp = fixture.componentInstance;
      service = fixture.debugElement.injector.get(SelectorService);
    });

    describe('save', () => {
      it('Should call update service on save for existing entity', fakeAsync(() => {
        // GIVEN
        const entity = new Selector(123);
        spyOn(service, 'update').and.returnValue(of(new HttpResponse({ body: entity })));
        comp.updateForm(entity);
        // WHEN
        comp.save();
        tick(); // simulate async

        // THEN
        expect(service.update).toHaveBeenCalledWith(entity);
        expect(comp.isSaving).toEqual(false);
      }));

      it('Should call create service on save for new entity', fakeAsync(() => {
        // GIVEN
        const entity = new Selector();
        spyOn(service, 'create').and.returnValue(of(new HttpResponse({ body: entity })));
        comp.updateForm(entity);
        // WHEN
        comp.save();
        tick(); // simulate async

        // THEN
        expect(service.create).toHaveBeenCalledWith(entity);
        expect(comp.isSaving).toEqual(false);
      }));
    });
  });
});
