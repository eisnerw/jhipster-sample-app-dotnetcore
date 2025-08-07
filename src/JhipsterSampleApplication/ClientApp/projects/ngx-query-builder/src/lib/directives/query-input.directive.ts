import { Directive, Input, TemplateRef, inject } from '@angular/core';

@Directive({selector: '[queryInput]', standalone: false})
export class QueryInputDirective {
  template = inject<TemplateRef<any>>(TemplateRef);

  /** Unique name for query input type. */
  @Input()
  get queryInputType(): string { return this._type; }
  set queryInputType(value: string) {
    // If the directive is set without a type (updated programatically), then this setter will
    // trigger with an empty string and should not overwrite the programatically set value.
    if (!value) { return; }
    this._type = value;
  }
  private _type!: string;
}
