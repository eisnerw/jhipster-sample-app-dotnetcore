import { ElementRef, OnInit, ChangeDetectorRef, ViewEncapsulation, forwardRef, ChangeDetectionStrategy, NgZone} from '@angular/core';
import { Component, Renderer2, HostBinding } from '@angular/core';
import { MultiSelect, MultiSelectItem} from 'primeng/multiselect';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FilterService, PrimeNGConfig, OverlayService } from 'primeng/api';

export const MULTISELECT_VALUE_ACCESSOR: any = {
  provide: NG_VALUE_ACCESSOR,
  useExisting: forwardRef(() => EditableMultiSelectComponent),
  multi: true
};
@Component({
  selector: 'jhi-editable-multiselect',
  template: `
    <div
    #container
    [ngClass]="{ 'p-multiselect p-component': true, 'p-multiselect-open': overlayVisible, 'p-multiselect-chip': display === 'chip', 'p-focus': focus, 'p-disabled': disabled }"
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
    <div class="p-multiselect-label-container" [pTooltip]="tooltip" [tooltipPosition]="tooltipPosition" [positionStyle]="tooltipPositionStyle" [tooltipStyleClass]="tooltipStyleClass">
        <div
            class="p-multiselect-label"
            [ngClass]="{ 'p-placeholder': valuesAsString === (defaultLabel || placeholder), 'p-multiselect-label-empty': (valuesAsString == null || valuesAsString.length === 0) && (placeholder == null || placeholder.length === 0) }"
        >
            <ng-container *ngIf="!selectedItemsTemplate">
                <ng-container *ngIf="display === 'comma'">{{ valuesAsString || 'empty' }}</ng-container>
                <ng-container *ngIf="display === 'chip'">
                    <div #token *ngFor="let item of value; let i = index" class="p-multiselect-token">
                        <span class="p-multiselect-token-label">{{ findLabelByValue(item) }}</span>
                        <span *ngIf="!disabled" class="p-multiselect-token-icon pi pi-times-circle" (click)="removeChip(item, $event)"></span>
                    </div>
                    <ng-container *ngIf="!value || value.length === 0">{{ placeholder || defaultLabel || 'empty' }}</ng-container>
                </ng-container>
            </ng-container>
            <ng-container *ngTemplateOutlet="selectedItemsTemplate; context: { $implicit: value }"></ng-container>
        </div>
        <i *ngIf="value != null && filled && !disabled && showClear" class="p-multiselect-clear-icon pi pi-times" (click)="clear($event)"></i>
    </div>
    <div [ngClass]="{ 'p-multiselect-trigger': true }">
        <span class="p-multiselect-trigger-icon" [ngClass]="dropdownIcon"></span>
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
            <div [ngClass]="['p-multiselect-panel p-component']" [ngStyle]="panelStyle" [class]="panelStyleClass" (keydown)="onKeydown($event)">
                <div class="p-multiselect-header" *ngIf="showHeader">
                    <ng-content select="p-header"></ng-content>
                    <ng-container *ngTemplateOutlet="headerTemplate"></ng-container>
                    <ng-container *ngIf="filterTemplate; else builtInFilterElement">
                        <ng-container *ngTemplateOutlet="filterTemplate; context: { options: filterOptions }"></ng-container>
                    </ng-container>
                    <ng-template #builtInFilterElement>
                        <div class="p-checkbox p-component" *ngIf="showToggleAll && !selectionLimit" [ngClass]="{ 'p-checkbox-disabled': disabled || toggleAllDisabled }">
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
                                [ngClass]="{ 'p-highlight': allChecked, 'p-focus': headerCheckboxFocus, 'p-disabled': disabled || toggleAllDisabled }"
                                (click)="toggleAll($event)"
                            >
                                <span class="p-checkbox-icon" [ngClass]="{ 'pi pi-check': allChecked }"></span>
                            </div>
                        </div>
                        <div class="p-multiselect-filter-container" *ngIf="filter">
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
                            <span class="p-multiselect-filter-icon pi pi-search"></span>
                        </div>
                        <button class="p-multiselect-close p-link" type="button" (click)="close($event)" pRipple>
                            <span class="p-multiselect-close-icon pi pi-times"></span>
                        </button>
                    </ng-template>
                </div>
                <div class="p-multiselect-items-wrapper" [style.max-height]="virtualScroll ? 'auto' : scrollHeight || 'auto'">
                    <p-scroller
                        *ngIf="virtualScroll"
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
                        <ng-template pTemplate="content" let-items let-scrollerOptions="options">
                            <ng-container *ngTemplateOutlet="buildInItems; context: { $implicit: items, options: scrollerOptions }"></ng-container>
                        </ng-template>
                        <ng-container *ngIf="loaderTemplate">
                            <ng-template pTemplate="loader" let-scrollerOptions="options">
                                <ng-container *ngTemplateOutlet="loaderTemplate; context: { options: scrollerOptions }"></ng-container>
                            </ng-template>
                        </ng-container>
                    </p-scroller>
                    <ng-container *ngIf="!virtualScroll">
                        <ng-container *ngTemplateOutlet="buildInItems; context: { $implicit: optionsToRender, options: {} }"></ng-container>
                    </ng-container>

                    <ng-template #buildInItems let-items let-scrollerOptions="options">
                        <ul #items class="p-multiselect-items p-component" [ngClass]="scrollerOptions.contentStyleClass" [style]="scrollerOptions.contentStyle" role="listbox" aria-multiselectable="true">
                            <ng-container *ngIf="group">
                                <ng-template ngFor let-optgroup [ngForOf]="items">
                                    <li class="p-multiselect-item-group" [ngStyle]="{ height: scrollerOptions.itemSize + 'px' }">
                                        <span *ngIf="!groupTemplate">{{ getOptionGroupLabel(optgroup) || 'empty' }}</span>
                                        <ng-container *ngTemplateOutlet="groupTemplate; context: { $implicit: optgroup }"></ng-container>
                                    </li>
                                    <ng-container *ngTemplateOutlet="itemslist; context: { $implicit: getOptionGroupChildren(optgroup) }"></ng-container>
                                </ng-template>
                            </ng-container>
                            <ng-container *ngIf="!group">
                                <ng-container *ngTemplateOutlet="itemslist; context: { $implicit: items }"></ng-container>
                            </ng-container>
                            <ng-template #itemslist let-optionsToDisplay let-selectedOption="selectedOption">
                                <ng-template ngFor let-option let-i="index" [ngForOf]="optionsToDisplay">
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
                                </ng-template>
                            </ng-template>
                            <li *ngIf="hasFilter() && isEmpty()" class="p-multiselect-empty-message" [ngStyle]="{ height: scrollerOptions.itemSize + 'px' }">
                                <ng-container>
                                    {{ emptyFilterMessageLabel }}
                                </ng-container>
                            </li>
                            <li *ngIf="!hasFilter() && isEmpty()" class="p-multiselect-empty-message" [ngStyle]="{ height: scrollerOptions.itemSize + 'px' }">
                                <ng-container>
                                    {{ emptyMessageLabel }}
                                </ng-container>
                            </li>
                        </ul>
                    </ng-template>
                </div>
                <div class="p-multiselect-footer" *ngIf="footerFacet || footerTemplate">
                    <ng-content select="p-footer"></ng-content>
                    <ng-container *ngTemplateOutlet="footerTemplate"></ng-container>
                </div>
            </div>
        </ng-template>
    </p-overlay>
  </div>
`,
  providers: [MULTISELECT_VALUE_ACCESSOR],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class EditableMultiSelectComponent extends MultiSelect implements OnInit{
  selected : string[] = [];
  newOption = '';

  get toggleAllDisabled():boolean{
    if (this.value.length === 0 && !this._filterValue){
      return true;
    }
    return false;
  }
  constructor(public el: ElementRef, public renderer: Renderer2, public cd: ChangeDetectorRef, public zone: NgZone, public filterService: FilterService, public config: PrimeNGConfig, overlayService: OverlayService   ) {
    super(el, renderer, cd, zone, filterService, config, overlayService);

  }
  ngOnInit(): void{
    super.ngOnInit();
  }

  // the following is necessary to prevent a type mismatch on event
  onFilterInputChange(event : any): void{
    const previousValue = this._filterValue;
    super.onFilterInputChange(event);
    if (this.newOption){
      this.value.length--;
      this.newOption = "";
    }
    if (this._filterValue !== previousValue && this._filterValue.length > 0){
      const searchFields = (this.filterBy || this.optionLabel || 'label').split(',');
      const matched = this.filterService.filter(this.options, searchFields, this._filterValue, 'equals', this.filterLocale);
      if (matched.length !== 1){
        let label = '';        
        for (let i = 0; i < this.value.length; i++) {
          const itemLabel = this.findLabelByValue(this.value[i]);
          if (itemLabel) {
            if (label.length > 0) {
                label = label + ', ';
            }
            label = label + itemLabel;
          }
        }          
        this.newOption = this._filterValue;
        const option : {[key : string] : string} = {};
        option[this.optionLabel] = this._filterValue;        
        this.value.push(option);
        this.valuesAsString =  label + (label === "" ? "" : ", ") + this.newOption;
      } else {
        this.updateLabel();
      }
    }
    this.onModelChange(this.value);
    this.onChange.emit({ originalEvent: event, value: this.value });
  }
  onKeydown(event : any) : void {
    if (event.which === 13) {
      this.toggleAll(event);
      return;
    } else if (event.which === 9 ){
      const searchFields = (this.filterBy || this.optionLabel || 'label').split(',');
      const matched = this.filterService.filter(this.options, searchFields, this._filterValue, 'contains', this.filterLocale);
      if (matched.length === 1){
        this.filterValue = matched[0][this.optionLabel];
        event.preventDefault();
        event.stopPropagation();
        return;
      }
    }
    super.onKeydown(event);
    return;
    const previousValue = this.filterValue;
    const alias = {
      _this: this
    }
    setTimeout(function() : void{
      const _this = alias._this;
      if (_this._filterValue !== previousValue){
        const value : {[key:string] : string} = {};
        value[_this.optionLabel] = _this._filterValue;        
        if (_this.value.length === 0 || _this.value[_this.value.length - 1] !== previousValue){
          _this.value.push(value);
        } else {
          _this.value[_this.value.length - 1] = value;
        }
        _this.onModelChange(_this.value);
        _this.onChange.emit({ originalEvent: event, value: _this.value });
        _this.valuesAsString += (", " + _this._filterValue);
      }
    }, 0);
  }
  toggleAll(event : any): void{
    // toggleAllDisabled is overloaded to add values
    const searchFields = (this.filterBy || this.optionLabel || 'label').split(',');
    const matched = this.filterService.filter(this.options, searchFields, this._filterValue, 'equals', this.filterLocale);
    if (matched.length === 1){
      const optionClickParm : {[key:string] : any} = {};
      optionClickParm["option"] = matched[0];
      this.onOptionClick(optionClickParm);
      this.filterValue = "";
      return;      
    }
    if (!this._filterValue){
      let filteringAllSelected = !!this._filteredOptions;
      // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
      if (this._filteredOptions){
        this._filteredOptions.forEach(o=>{
          if (!this.isSelected(o)){
            filteringAllSelected = false;
          }
        });
      }
      // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
      if (filteringAllSelected){
        (this._filteredOptions as unknown) = null;
        this._filterValue = "";
      } else if (this.value.length > 0) {
        this._filteredOptions = [];
        this._options.forEach(o=>{
          if (this.isSelected(o)){
            this._filteredOptions.push(o);
          }
        });
      }
      return;
    } else if (event) { // (testing for event in order to avoid a lint warning)
      // add this value to options
      const option : {[key:string] : string} = {};
      option[this.optionLabel] = this._filterValue
      this.options.unshift(option);
      const optionClickParm : {[key:string] : {[key:string] : string} }= {};
      optionClickParm["option"] = option;
      this.onOptionClick(optionClickParm);
      this._filterValue = "";
      this.activateFilter();
    }
  }
  close(input: any) :void{
    this._filterValue = "";
    this.activateFilter();
    input.focus();
    if (this.newOption){
      this.value.length--;
      this.newOption = "";
      this.updateLabel();
    }
  }
}
@Component({
    selector: 'jhi-editable-multiSelectItem',
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
                <span class="p-checkbox-icon" [ngClass]="{ 'pi pi-check': selected }"></span>
            </div>
        </div>
        <span *ngIf="!template">{{ label }}</span>
        <ng-container *ngTemplateOutlet="template; context: { $implicit: option }"></ng-container>
      </li>
`,
    encapsulation: ViewEncapsulation.None,
})
export class MultiSelectItemComponent extends MultiSelectItem {
  @HostBinding('class') role = 'p-element';  
}
