import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { HttpHeaders, HttpResponse } from '@angular/common/http';

import { JhipsterTestModule } from '../../../test.module';
import { SelectorComponent } from 'app/entities/selector/selector.component';
import { SelectorService } from 'app/entities/selector/selector.service';
import { Selector } from 'app/shared/model/selector.model';

describe('Component Tests', () => {
  describe('Selector Management Component', () => {
    let comp: SelectorComponent;
    let fixture: ComponentFixture<SelectorComponent>;
    let service: SelectorService;

    beforeEach(() => {
      TestBed.configureTestingModule({
        imports: [JhipsterTestModule],
        declarations: [SelectorComponent],
      })
        .overrideTemplate(SelectorComponent, '')
        .compileComponents();

      fixture = TestBed.createComponent(SelectorComponent);
      comp = fixture.componentInstance;
      service = fixture.debugElement.injector.get(SelectorService);
    });

    it('Should call load all on init', () => {
      // GIVEN
      const headers = new HttpHeaders().append('link', 'link;link');
      spyOn(service, 'query').and.returnValue(
        of(
          new HttpResponse({
            body: [new Selector(123)],
            headers,
          })
        )
      );

      // WHEN
      comp.ngOnInit();

      // THEN
      expect(service.query).toHaveBeenCalled();
      expect(comp.selectors && comp.selectors[0]).toEqual(jasmine.objectContaining({ id: 123 }));
    });
  });
});
