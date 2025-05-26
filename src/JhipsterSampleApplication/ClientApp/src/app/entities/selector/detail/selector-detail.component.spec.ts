import { ComponentFixture, TestBed } from "@angular/core/testing";
import { ActivatedRoute } from "@angular/router";
import { of } from "rxjs";

import { SelectorDetailComponent } from "./selector-detail.component";

describe("Component Tests", () => {
  describe("Selector Management Detail Component", () => {
    let comp: SelectorDetailComponent;
    let fixture: ComponentFixture<SelectorDetailComponent>;

    beforeEach(() => {
      TestBed.configureTestingModule({
        declarations: [SelectorDetailComponent],
        providers: [
          {
            provide: ActivatedRoute,
            useValue: { data: of({ selector: { id: 123 } }) },
          },
        ],
      })
        .overrideTemplate(SelectorDetailComponent, "")
        .compileComponents();
      fixture = TestBed.createComponent(SelectorDetailComponent);
      comp = fixture.componentInstance;
    });

    describe("OnInit", () => {
      it("Should load selector on init", () => {
        // WHEN
        comp.ngOnInit();

        // THEN
        expect(comp.selector).toEqual(expect.objectContaining({ id: 123 }));
      });
    });
  });
});
