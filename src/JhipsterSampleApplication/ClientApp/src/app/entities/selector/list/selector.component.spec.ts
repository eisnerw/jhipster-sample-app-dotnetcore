import { ComponentFixture, TestBed } from "@angular/core/testing";
import { HttpHeaders, HttpResponse } from "@angular/common/http";
import { HttpClientTestingModule } from "@angular/common/http/testing";
import { of } from "rxjs";

import { SelectorService } from "../service/selector.service";

import { SelectorComponent } from "./selector.component";

describe("Component Tests", () => {
  describe("Selector Management Component", () => {
    let comp: SelectorComponent;
    let fixture: ComponentFixture<SelectorComponent>;
    let service: SelectorService;

    beforeEach(() => {
      TestBed.configureTestingModule({
        imports: [HttpClientTestingModule],
        declarations: [SelectorComponent],
      })
        .overrideTemplate(SelectorComponent, "")
        .compileComponents();

      fixture = TestBed.createComponent(SelectorComponent);
      comp = fixture.componentInstance;
      service = TestBed.inject(SelectorService);

      const headers = new HttpHeaders().append("link", "link;link");
      jest.spyOn(service, "query").mockReturnValue(
        of(
          new HttpResponse({
            body: [{ id: 123 }],
            headers,
          })
        )
      );
    });

    it("Should call load all on init", () => {
      // WHEN
      comp.ngOnInit();

      // THEN
      expect(service.query).toHaveBeenCalled();
      expect(comp.selectors?.[0]).toEqual(expect.objectContaining({ id: 123 }));
    });
  });
});
