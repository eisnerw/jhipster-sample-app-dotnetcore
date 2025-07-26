import { Directive, ElementRef, Input, AfterViewInit, OnDestroy } from '@angular/core';

@Directive({
  selector: '[jhiPersistColumnWidths]',
})
export class ColumnWidthDirective implements AfterViewInit, OnDestroy {
  @Input('jhiPersistColumnWidths') key = '';

  constructor(private el: ElementRef<HTMLTableElement>) {}

  ngAfterViewInit(): void {
    if (!this.key) return;
    const stored = localStorage.getItem(`column-widths:${this.key}`);
    if (stored) {
      try {
        const widths = JSON.parse(stored) as number[];
        const ths = this.el.nativeElement.querySelectorAll('th');
        ths.forEach((th, i) => {
          const width = widths[i];
          if (width) {
            (th as HTMLElement).style.width = `${width}px`;
          }
        });
      } catch {
        // ignore parse errors
      }
    }
  }

  ngOnDestroy(): void {
    if (!this.key) return;
    const ths = this.el.nativeElement.querySelectorAll('th');
    const widths = Array.from(ths).map(th => (th as HTMLElement).offsetWidth);
    localStorage.setItem(`column-widths:${this.key}`, JSON.stringify(widths));
  }
}
