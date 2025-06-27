import { Directive, ElementRef, EventEmitter, NgZone, OnDestroy, OnInit, Output } from '@angular/core';

/**
 * Directive that monitors scroll position of a group table and emits the first visible group name.
 * It is intended to be used on the table element when the table is in "group" mode.
 */
@Directive({
  selector: '[jhiGroupScrollMonitor]',
  standalone: true,
})
export class GroupScrollMonitorDirective implements OnInit, OnDestroy {
  /** Emits the group name of the first visible row */
  @Output() visibleGroupChange = new EventEmitter<string>();

  private observer?: IntersectionObserver;
  private groupRows: HTMLElement[] = [];

  constructor(
    private elementRef: ElementRef<HTMLElement>,
    private ngZone: NgZone,
  ) {}

  ngOnInit(): void {
    // defer observer creation to the Angular zone to avoid change detection thrashing
    this.ngZone.runOutsideAngular(() => {
      this.groupRows = Array.from(this.elementRef.nativeElement.querySelectorAll<HTMLElement>('[data-group]'));
      this.observer = new IntersectionObserver(
        entries => {
          // pick the first visible group row
          const visible = entries
            .filter(e => e.isIntersecting && e.boundingClientRect.top >= 0)
            .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)
            .shift();
          if (visible) {
            const group = visible.target.getAttribute('data-group');
            if (group) {
              // emit inside the Angular zone so components can update state
              this.ngZone.run(() => this.visibleGroupChange.emit(group));
            }
          }
        },
        { root: this.elementRef.nativeElement, threshold: [0] },
      );

      this.groupRows.forEach(row => this.observer!.observe(row));
    });
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }
}
