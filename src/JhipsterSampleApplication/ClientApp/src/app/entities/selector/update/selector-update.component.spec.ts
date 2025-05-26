jest.mock("@angular/router");

import { ComponentFixture, TestBed } from "@angular/core/testing";
import { HttpResponse } from "@angular/common/http";
import { HttpClientTestingModule } from "@angular/common/http/testing";
import { FormBuilder } from "@angular/forms";
import { ActivatedRoute } from "@angular/router";
import { of, Subject } from "rxjs";

import { SelectorService } from "../service/selector.service";
import { ISelector, Selector } from "../selector.model";

import { SelectorUpdateComponent } from "./selector-update.component";

describe("Component Tests", () => {
  describe("Selector Management Update Component", () => {
    let comp: SelectorUpdateComponent;
    let fixture: ComponentFixture<SelectorUpdateComponent>;
    let activatedRoute: ActivatedRoute;
    let selectorService: SelectorService;

    beforeEach(() => {
      TestBed.configureTestingModule({
        imports: [HttpClientTestingModule],
        declarations: [SelectorUpdateComponent],
        providers: [FormBuilder, ActivatedRoute],
      })
        .overrideTemplate(SelectorUpdateComponent, "")
        .compileComponents();

      fixture = TestBed.createComponent(SelectorUpdateComponent);
      activatedRoute = TestBed.inject(ActivatedRoute);
      selectorService = TestBed.inject(SelectorService);

      comp = fixture.componentInstance;
    });

    describe("ngOnInit", () => {
      it("Should update editForm", () => {
        const selector: ISelector = { id: 456 };

        activatedRoute.data = of({ selector });
        comp.ngOnInit();

        expect(comp.editForm.value).toEqual(expect.objectContaining(selector));
      });
    });

    describe("save", () => {
      it("Should call update service on save for existing entity", () => {
        // GIVEN
        const saveSubject = new Subject<HttpResponse<Selector>>();
        const selector = { id: 123 };
        jest.spyOn(selectorService, "update").mockReturnValue(saveSubject);
        jest.spyOn(comp, "previousState");
        activatedRoute.data = of({ selector });
        comp.ngOnInit();

        // WHEN
        comp.save();
        expect(comp.isSaving).toEqual(true);
        saveSubject.next(new HttpResponse({ body: selector }));
        saveSubject.complete();

        // THEN
        expect(comp.previousState).toHaveBeenCalled();
        expect(selectorService.update).toHaveBeenCalledWith(selector);
        expect(comp.isSaving).toEqual(false);
      });

      it("Should call create service on save for new entity", () => {
        // GIVEN
        const saveSubject = new Subject<HttpResponse<Selector>>();
        const selector = new Selector();
        jest.spyOn(selectorService, "create").mockReturnValue(saveSubject);
        jest.spyOn(comp, "previousState");
        activatedRoute.data = of({ selector });
        comp.ngOnInit();

        // WHEN
        comp.save();
        expect(comp.isSaving).toEqual(true);
        saveSubject.next(new HttpResponse({ body: selector }));
        saveSubject.complete();

        // THEN
        expect(selectorService.create).toHaveBeenCalledWith(selector);
        expect(comp.isSaving).toEqual(false);
        expect(comp.previousState).toHaveBeenCalled();
      });

      it("Should set isSaving to false on error", () => {
        // GIVEN
        const saveSubject = new Subject<HttpResponse<Selector>>();
        const selector = { id: 123 };
        jest.spyOn(selectorService, "update").mockReturnValue(saveSubject);
        jest.spyOn(comp, "previousState");
        activatedRoute.data = of({ selector });
        comp.ngOnInit();

        // WHEN
        comp.save();
        expect(comp.isSaving).toEqual(true);
        saveSubject.error("This is an error!");

        // THEN
        expect(selectorService.update).toHaveBeenCalledWith(selector);
        expect(comp.isSaving).toEqual(false);
        expect(comp.previousState).not.toHaveBeenCalled();
      });
    });
  });
});
