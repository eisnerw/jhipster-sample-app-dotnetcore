import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { JhipsterTestModule } from '../../../test.module';
import { SelectorDetailComponent } from 'app/entities/selector/selector-detail.component';
import { Selector } from 'app/shared/model/selector.model';

describe('Component Tests', () => {
  describe('Selector Management Detail Component', () => {
    let comp: SelectorDetailComponent;
    let fixture: ComponentFixture<SelectorDetailComponent>;
    const route = ({ data: of({ selector: new Selector(123) }) } as any) as ActivatedRoute;

    beforeEach(() => {
      TestBed.configureTestingModule({
        imports: [JhipsterTestModule],
        declarations: [SelectorDetailComponent],
        providers: [{ provide: ActivatedRoute, useValue: route }],
      })
        .overrideTemplate(SelectorDetailComponent, '')
        .compileComponents();
      fixture = TestBed.createComponent(SelectorDetailComponent);
      comp = fixture.componentInstance;
    });

    describe('OnInit', () => {
      it('Should load selector on init', () => {
        // WHEN
        comp.ngOnInit();

        // THEN
        expect(comp.selector).toEqual(jasmine.objectContaining({ id: 123 }));
      });
    });
  });
});
