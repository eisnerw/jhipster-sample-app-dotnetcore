/* eslint-disable @typescript-eslint/no-unnecessary-condition */
import {
  ElementRef,
  OnInit,
  ChangeDetectorRef,
  ViewEncapsulation,
  forwardRef,
  ChangeDetectionStrategy,
  NgZone,
} from '@angular/core';
import { Component, Renderer2, HostBinding } from '@angular/core';
import { MultiSelect, MultiSelectItem } from 'primeng/multiselect';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FilterService, OverlayService } from 'primeng/api';
import { signal, WritableSignal } from '@angular/core';

export const MULTISELECT_VALUE_ACCESSOR: any = {
  provide: NG_VALUE_ACCESSOR,
  useExisting: forwardRef(() => EditableMultiSelectComponent),
  multi: true,
};

@Component({
  selector: 'jhi-editable-multi-select',
  template: `
    <div
      #container
      [ngClass]="{
        'p-multiselect p-component': true,
        'p-multiselect-open': overlayVisible,
        'p-multiselect-chip': display === 'chip',
        'p-focus': focus,
        'p-disabled': disabled,
      }"
      [ngStyle]="style"
      [class]="styleClass"
      (click)="onMouseclick($event, in)"
    >
      <div class="p-hidden-accessible">
        <input
          #in
          type="text"
          [attr.label]="label"
          readonly="readonly"
          [attr.id]="inputId"
          [attr.name]="name"
          (focus)="onInputFocus($event)"
          (blur)="onInputBlur($event)"
          [disabled]="disabled"
          [attr.tabindex]="tabindex"
          (keydown)="onKeydown($event)"
          aria-haspopup="listbox"
          [attr.aria-expanded]="overlayVisible"
          [attr.aria-labelledby]="ariaLabelledBy"
          role="listbox"
        />
      </div>
      <div
        class="p-multiselect-label-container"
        [pTooltip]="tooltip"
        [tooltipPosition]="tooltipPosition"
        [positionStyle]="tooltipPositionStyle"
        [tooltipStyleClass]="tooltipStyleClass"
      >
        <div
          class="p-multiselect-label"
          [ngClass]="{
            'p-placeholder': valuesAsString === (defaultLabel || placeholder),
            'p-multiselect-label-empty':
              (valuesAsString == null || valuesAsString.length === 0) &&
              (placeholder == null || placeholder.length === 0),
          }"
        >
          @if (!selectedItemsTemplate) {
            @if (display === 'comma') {
              {{ valuesAsString || 'empty' }}
            }
            @if (display === 'chip') {
              @for (item of value; track item; let i = $index) {
                <div #token class="p-multiselect-token">
                  <span class="p-multiselect-token-label">{{
                    findLabelByValue(item)
                  }}</span>
                  @if (!disabled) {
                    <span
                      class="p-multiselect-token-icon pi pi-times-circle"
                      (click)="removeChip(item, $event)"
                    ></span>
                  }
                </div>
              }
              @if (!value || value.length === 0) {
                {{ placeholder || defaultLabel || 'empty' }}
              }
            }
          }
          <ng-container
            *ngTemplateOutlet="
              selectedItemsTemplate;
              context: { $implicit: value }
            "
          ></ng-container>
        </div>
        @if (value != null && filled && !disabled && showClear) {
          <i
            class="p-multiselect-clear-icon pi pi-times"
            (click)="clear($event)"
          ></i>
        }
      </div>
      <div [ngClass]="{ 'p-multiselect-trigger': true }">
        <span
          class="p-multiselect-trigger-icon"
          [ngClass]="dropdownIcon"
        ></span>
      </div>
      <p-overlay
        #overlay
        [(visible)]="overlayVisible"
        [options]="overlayOptions"
        [target]="'@parent'"
        [appendTo]="appendTo"
        [autoZIndex]="autoZIndex"
        [baseZIndex]="baseZIndex"
        [showTransitionOptions]="showTransitionOptions"
        [hideTransitionOptions]="hideTransitionOptions"
        (onAnimationStart)="onOverlayAnimationStart($event)"
        (onHide)="hide()"
      >
        <ng-template pTemplate="content">
          <div
            [ngClass]="['p-multiselect-panel p-component']"
            [ngStyle]="panelStyle"
            [class]="panelStyleClass"
            (keydown)="onKeydown($event)"
          >
            @if (showHeader) {
              <div class="p-multiselect-header">
                <ng-content select="p-header"></ng-content>
                <ng-container *ngTemplateOutlet="headerTemplate"></ng-container>
                @if (filterTemplate) {
                  <ng-container
                    *ngTemplateOutlet="
                      filterTemplate;
                      context: { options: filterOptions }
                    "
                  ></ng-container>
                } @else {
                  @if (showToggleAll && !selectionLimit) {
                    <div
                      class="p-checkbox p-component"
                      [ngClass]="{
                        'p-checkbox-disabled': disabled || toggleAllDisabled,
                      }"
                    >
                      <div class="p-hidden-accessible">
                        <input
                          type="checkbox"
                          readonly="readonly"
                          [checked]="allChecked"
                          (focus)="onHeaderCheckboxFocus()"
                          (blur)="onHeaderCheckboxBlur()"
                          (keydown.space)="toggleAll($event)"
                          [disabled]="disabled || toggleAllDisabled"
                        />
                      </div>
                      <div
                        class="p-checkbox-box"
                        role="checkbox"
                        [attr.aria-checked]="allChecked"
                        [ngClass]="{
                          'p-highlight': allChecked,
                          'p-focus': headerCheckboxFocus,
                          'p-disabled': disabled || toggleAllDisabled,
                        }"
                        (click)="toggleAll($event)"
                      >
                        <span
                          class="p-checkbox-icon"
                          [ngClass]="{ 'pi pi-check': allChecked }"
                        ></span>
                      </div>
                    </div>
                  }
                  @if (filter) {
                    <div class="p-multiselect-filter-container">
                      <input
                        #filterInput
                        type="text"
                        [attr.autocomplete]="autocomplete"
                        role="textbox"
                        [value]="filterValue || ''"
                        (input)="onFilterInputChange($event)"
                        class="p-multiselect-filter p-inputtext p-component"
                        [disabled]="disabled"
                        [attr.placeholder]="filterPlaceHolder"
                        [attr.aria-label]="ariaFilterLabel"
                      />
                      <span
                        class="p-multiselect-filter-icon pi pi-search"
                      ></span>
                    </div>
                  }
                  <button
                    class="p-multiselect-close p-link"
                    type="button"
                    (click)="close($event)"
                    pRipple
                  >
                    <span class="p-multiselect-close-icon pi pi-times"></span>
                  </button>
                }
              </div>
            }
            <div
              class="p-multiselect-items-wrapper"
              [style.max-height]="
                virtualScroll ? 'auto' : scrollHeight || 'auto'
              "
            >
              @if (virtualScroll) {
                <p-scroller
                  #scroller
                  [items]="optionsToRender"
                  [style]="{ height: scrollHeight }"
                  [itemSize]="virtualScrollItemSize || _itemSize"
                  [autoSize]="true"
                  [tabindex]="-1"
                  [lazy]="lazy"
                  (onLazyLoad)="onLazyLoad.emit($event)"
                  [options]="virtualScrollOptions"
                >
                  <ng-template
                    pTemplate="content"
                    let-items
                    let-scrollerOptions="options"
                  >
                    <ng-container
                      *ngTemplateOutlet="
                        buildInItems;
                        context: { $implicit: items, options: scrollerOptions }
                      "
                    ></ng-container>
                  </ng-template>
                  @if (loaderTemplate) {
                    <ng-template
                      pTemplate="loader"
                      let-scrollerOptions="options"
                    >
                      <ng-container
                        *ngTemplateOutlet="
                          loaderTemplate;
                          context: { options: scrollerOptions }
                        "
                      ></ng-container>
                    </ng-template>
                  }
                </p-scroller>
              }
              @if (!virtualScroll) {
                <ng-container
                  *ngTemplateOutlet="
                    buildInItems;
                    context: { $implicit: optionsToRender, options: {} }
                  "
                ></ng-container>
              }

              <ng-template
                #buildInItems
                let-items
                let-scrollerOptions="options"
              >
                <ul
                  #items
                  class="p-multiselect-items p-component"
                  [ngClass]="scrollerOptions.contentStyleClass"
                  [style]="scrollerOptions.contentStyle"
                  role="listbox"
                  aria-multiselectable="true"
                >
                  @if (group) {
                    @for (optgroup of items; track optgroup) {
                      <li
                        class="p-multiselect-item-group"
                        [ngStyle]="{ height: scrollerOptions.itemSize + 'px' }"
                      >
                        @if (!groupTemplate) {
                          <span>{{
                            getOptionGroupLabel(optgroup) || 'empty'
                          }}</span>
                        }
                        <ng-container
                          *ngTemplateOutlet="
                            groupTemplate;
                            context: { $implicit: optgroup }
                          "
                        ></ng-container>
                      </li>
                      <ng-container
                        *ngTemplateOutlet="
                          itemslist;
                          context: {
                            $implicit: getOptionGroupChildren(optgroup),
                          }
                        "
                      ></ng-container>
                    }
                  }
                  @if (!group) {
                    <ng-container
                      *ngTemplateOutlet="
                        itemslist;
                        context: { $implicit: items }
                      "
                    ></ng-container>
                  }
                  <ng-template
                    #itemslist
                    let-optionsToDisplay
                    let-selectedOption="selectedOption"
                  >
                    @for (
                      option of optionsToDisplay;
                      track option;
                      let i = $index
                    ) {
                      <jhi-editable-multiSelectItem
                        [option]="option"
                        [selected]="isSelected(option)"
                        [label]="getOptionLabel(option)"
                        [disabled]="isOptionDisabled(option)"
                        (onClick)="onOptionClick($event)"
                        (onKeydown)="onOptionKeydown($event)"
                        [template]="itemTemplate"
                        [itemSize]="scrollerOptions.itemSize"
                      ></jhi-editable-multiSelectItem>
                    }
                  </ng-template>
                  @if (hasFilter() && isEmpty()) {
                    <li
                      class="p-multiselect-empty-message"
                      [ngStyle]="{ height: scrollerOptions.itemSize + 'px' }"
                    >
                      <ng-container>
                        {{ emptyFilterMessageLabel }}
                      </ng-container>
                    </li>
                  }
                  @if (!hasFilter() && isEmpty()) {
                    <li
                      class="p-multiselect-empty-message"
                      [ngStyle]="{ height: scrollerOptions.itemSize + 'px' }"
                    >
                      <ng-container>
                        {{ emptyMessageLabel }}
                      </ng-container>
                    </li>
                  }
                </ul>
              </ng-template>
            </div>
            @if (footerFacet || footerTemplate) {
              <div class="p-multiselect-footer">
                <ng-content select="p-footer"></ng-content>
                <ng-container *ngTemplateOutlet="footerTemplate"></ng-container>
              </div>
            }
          </div>
        </ng-template>
      </p-overlay>
    </div>
  `,
  providers: [MULTISELECT_VALUE_ACCESSOR],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class EditableMultiSelectComponent
  extends MultiSelect
  implements OnInit
{
  selected: string[] = [];
  newOption = '';
  displayField = 'label';
  valueField = 'value';
  public override _filterValue: WritableSignal<string> = signal('');
  public override _filteredOptions: unknown[] = [];
  private _items: WritableSignal<Record<string, unknown>[]> = signal([]);
  private _selectedItems: WritableSignal<Record<string, unknown>[]> = signal(
    [],
  );

  get toggleAllDisabled(): boolean {
    return this.value.length === 0 && !this._filterValue();
  }

  ngOnInit(): void {
    super.ngOnInit();
  }

  items(): Record<string, unknown>[] {
    return this._items();
  }

  selectedItems(): Record<string, unknown>[] {
    return this._selectedItems();
  }

  findLabelByValue(value: unknown): string {
    const foundItem = this.items().find(
      (item) => this.getValue(item) === value,
    );
    return foundItem ? this.getDisplayValue(foundItem) : '';
  }

  updateLabel(): void {
    this.valuesAsString = this.getSelectedDisplayValues();
  }

  onOptionClick(event: Event): void {
    // Custom logic or call super if needed
  }

  onFilterInputChange(event: Event): void {
    const previousValue = this._filterValue();
    super.onFilterInputChange(event);
    if (this.newOption) {
      this.value.length--;
      this.newOption = '';
    }
    if (
      this._filterValue() !== previousValue &&
      this._filterValue().length > 0
    ) {
      const searchFields = (this.filterBy ?? this.optionLabel ?? 'label').split(
        ',',
      );
      const matched = this.filterService.filter(
        this.options ?? [],
        searchFields,
        this._filterValue(),
        'equals',
        this.filterLocale,
      );
      if (matched.length !== 1) {
        let label = '';
        for (const item of this.value) {
          const itemLabel = this.findLabelByValue(item);
          if (itemLabel) {
            if (label.length > 0) {
              label = label + ', ';
            }
            label = label + itemLabel;
          }
        }
        this.newOption = this._filterValue();
        const option: Record<string, string> = {};
        option[this.optionLabel ?? 'label'] = this._filterValue();
        this.value.push(option);
        this.valuesAsString =
          label + (label === '' ? '' : ', ') + this.newOption;
      } else {
        this.updateLabel();
      }
    }
    this.onModelChange(this.value);
    this.onChange.emit({ originalEvent: event, value: this.value });
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      this.toggleAll(event);
      return;
    } else if (event.key === 'Tab') {
      const searchFields = (this.filterBy ?? this.optionLabel ?? 'label').split(
        ',',
      );
      const matched = this.filterService.filter(
        this.options ?? [],
        searchFields,
        this._filterValue(),
        'contains',
        this.filterLocale,
      );
      if (matched.length === 1) {
        this.filterValue = matched[0][this.optionLabel ?? 'label'];
        event.preventDefault();
        event.stopPropagation();
        return;
      }
    }
    super.onKeyDown(event);
  }

  toggleAll(event: Event): void {
    const searchFields = (this.filterBy ?? this.optionLabel ?? 'label').split(
      ',',
    );
    const matched = this.filterService.filter(
      this.options ?? [],
      searchFields,
      this._filterValue(),
      'equals',
      this.filterLocale,
    );
    if (matched.length === 1) {
      this.onOptionSelect({ originalEvent: event, option: matched[0] });
      this.filterValue = '';
      return;
    }
    if (!this._filterValue()) {
      const filteringAllSelected = this._filteredOptions.every((o) =>
        this.isSelected(o),
      );
      if (this._filteredOptions.length > 0 && filteringAllSelected) {
        this._filteredOptions = [];
        this._filterValue.set('');
      } else if (this.value.length > 0) {
        this._filteredOptions = [];
        if (this.options) {
          this.options.forEach((o: unknown) => {
            if (this.isSelected(o)) {
              this._filteredOptions.push(o);
            }
          });
        }
      }
      return;
    } else if (event) {
      const option: Record<string, string> = {};
      option[this.optionLabel ?? 'label'] = this._filterValue();
      if (this.options) {
        this.options.unshift(option);
      }
      this.onOptionSelect({ originalEvent: event, option });
      this._filterValue.set('');
      this.activateFilter();
    }
  }

  close(event: Event): void {
    this._filterValue.set('');
    this.activateFilter();
    if (this.newOption) {
      this.value.length--;
      this.newOption = '';
      this.updateLabel();
    }
  }

  private updateSelectedItems(): void {
    const selectedItems = this.selectedItems();
    const items = this.items();
    for (const item of items) {
      item.selected = selectedItems.some((selected) => selected.id === item.id);
    }
  }

  private getDisplayValue(item: Record<string, unknown>): string {
    return typeof item[this.displayField] === 'string'
      ? (item[this.displayField] as string)
      : '';
  }

  private getValue(item: Record<string, unknown>): unknown {
    return item[this.valueField] ?? null;
  }

  private getItemById(id: unknown): Record<string, unknown> | null {
    return this.items().find((item) => this.getValue(item) === id) ?? null;
  }

  private getSelectedItems(): Record<string, unknown>[] {
    return this.items().filter((item) => item.selected);
  }

  private getSelectedValues(): unknown[] {
    return this.getSelectedItems().map((item) => this.getValue(item));
  }

  private getSelectedDisplayValues(): string {
    return this.getSelectedItems()
      .map((item) => this.getDisplayValue(item))
      .join(', ');
  }
}

@Component({
  selector: 'jhi-editable-multi-select-item',
  template: `
    <li
      class="p-multiselect-item"
      (click)="onOptionClick($event)"
      (keydown)="onOptionKeydown($event)"
      [attr.aria-label]="label"
      [attr.tabindex]="disabled ? null : '0'"
      [ngStyle]="{ height: itemSize + 'px' }"
      [ngClass]="{ 'p-highlight': selected, 'p-disabled': disabled }"
      pRipple
    >
      <div class="p-checkbox p-component">
        <div class="p-checkbox-box" [ngClass]="{ 'p-highlight': selected }">
          <span
            class="p-checkbox-icon"
            [ngClass]="{ 'pi pi-check': selected }"
          ></span>
        </div>
      </div>
      @if (!template) {
        <span>{{ label }}</span>
      }
      <ng-container
        *ngTemplateOutlet="template; context: { $implicit: option }"
      ></ng-container>
    </li>
  `,
  encapsulation: ViewEncapsulation.None,
})
export class MultiSelectItemComponent extends MultiSelectItem {
  @HostBinding('class') role = 'p-element';
}
