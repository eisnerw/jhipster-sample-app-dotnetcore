/* eslint-disable */ 
import { Component, OnInit, OnDestroy, Input, Output, EventEmitter } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { CommonModule, DatePipe } from '@angular/common';
import { TableModule } from 'primeng/table';
import { of } from 'rxjs';
import { SuperTable } from '../../../shared/SuperTable/super-table.component';
import { Sort } from 'app/shared/models/sort.model';
import { ConfirmationService, MessageService } from 'primeng/api';
import { MenuItem } from 'primeng/api';
import { DomSanitizer } from "@angular/platform-browser";
import { PrimeNGConfig } from "primeng/api";
import { faCheck } from '@fortawesome/free-solid-svg-icons';
import { ICategory } from '../../category/category.model';

@Component({
  selector: 'jhi-birthday-table',
  templateUrl: './birthday-table.component.html',
  providers: [MessageService, ConfirmationService]
})

export class BirthdayTableComponent implements OnInit, OnDestroy {
  birthdays?: IBirthday[];
  birthdaysMap : {[key: number] : IBirthday} = {};
  eventSubscriber?: Subscription;
  totalItems = 0;
  itemsPerPage = ITEMS_PER_PAGE;
  page!: number;
  predicate!: string;
  ascending!: boolean;
  ngbPaginationPage = 1;
  expandedRows : {[key:string]: boolean} = {};
  faCheck = faCheck;
  @Input() parent: SuperTable | null = null;
  @Input() refresh: any = null;

  columnDefs = [
    { field: 'lname', sortable: true, filter: true },
    { field: 'fname', sortable: true, filter: true },
    { field: 'dob', sortable: true, filter: true },
    { field: 'sign', headerName: 'sign', sortable: true, filter: true },
    { field: 'isAlive', sortable: true, filter: true },
  ];

  @Input() firstColumnIndent = "";

  @Input() hideHeader = false;

  rowData = new Observable<any[]>();

  menuItems: MenuItem[] = [];

  contextSelectedRow: IBirthday | null = null;

  checkboxSelectedRows : IBirthday[] = [];

  chipSelectedRows : object[] = [];

  bDisplaySearchDialog = false;

  bDisplayBirthday = false;

  bDisplayCategories = false;

  birthdayDialogTitle  = "";

  birthdayDialogId : any = "";

  @Input() databaseQuery = "";

  @Input() category: ICategory | null = null;

  @Input() categories : any = [];

  selectedCategories : ICategory[] = [];

  initialSelectedCategories = "";

  @Output() setViewFocus:EventEmitter<IBirthday> = new EventEmitter<IBirthday>();

  public loadingMessage = "";

  constructor(
    protected birthdayService: BirthdayService,
    protected categoryService: CategoryService,
    protected activatedRoute: ActivatedRoute,
    protected router: Router,
    protected modalService: NgbModal,
    protected messageService: MessageService,
    public sanitizer:DomSanitizer,
    private confirmationService: ConfirmationService,
    private primeNGConfig : PrimeNGConfig,
  ) {}

  getTableStyle(stdiv: HTMLDivElement):object{
    if (this.superTableParent && this.superTableParent.displayingAsCategories){
      return {};
    }
    const screenHeight = window.innerHeight;
    const divTop = stdiv.getBoundingClientRect().top;
    const FOOTERHEIGHT = 30;
    const divHeight = Math.floor(screenHeight - divTop - FOOTERHEIGHT);
    return {"overflow-y":"scroll","height": divHeight + "px"};
  }

  loadPage(page?: number, dontNavigate?: boolean): void {
    const pageToLoad: number = page || this.page || 1;

    const viewQuery: any = !this.parent || this.parent.selectedView === null ? {view: null} : {view:this.parent.selectedView};
    viewQuery.query = this.databaseQuery;
    viewQuery.category = this.category?.notCategorized ? "-" : this.category?.categoryName;
    if (this.category?.focusType !== "NONE"){
      viewQuery.focusType = this.category?.focusType;
      viewQuery.focusId = this.category?.focusId;
    }
    if (this.category?.jsonString){
      viewQuery.query = this.category.jsonString;
      viewQuery.view = null;
    }
    this.birthdayService
      .postQuery({
        page: pageToLoad - 1,
        size: this.itemsPerPage,
        sort: this.sort(),
        query: JSON.stringify(this.category?.ids && this.category.ids.length > 0 ? {ids:this.category.ids}: viewQuery)
      })
      .subscribe(
        (res: any) => this.onSuccess(res.body, res.headers, pageToLoad, !dontNavigate),
        () => this.onError()
      );
      this.loadingMessage = "Loading ...";
  }

  refreshData(): void {
    this.birthdays =[];
    this.rowData = of(this.birthdays);
    this.loadPage();
  }

  clearFilters(table: SuperTable, searchInput: any): void{
    searchInput.value = ""; // should clear filter
    table.reset();
    Object.keys(this.expandedRows).forEach((key)=>{
      this.expandedRows[key] = false;
    });
    table.filterGlobal(searchInput.value, 'contains');  // Not sure why this is necessary, but otherwise filter stays active
  }

  isDisplayingEllipsis(element : HTMLElement) : boolean{
    const tolerance = 3;
    return element.offsetWidth + tolerance < element.scrollWidth
  }

  onCheckboxChange() : void {
    this.chipSelectedRows = [];
    if (this.checkboxSelectedRows.length < 3){
      this.checkboxSelectedRows.forEach((row)=>{
        this.chipSelectedRows.push(row);
      });
    }
  }

  setMenu(birthday : any):void{
    if (this.menuItems[0]?.items) {
      this.menuItems[0].label = `Select action for ${birthday.fname} ${birthday.lname}`;
    }
    let alternate : any = null;
    this.chipSelectedRows.forEach((selectedRow)=>{
      if ((selectedRow as IBirthday).id !== birthday.id){
        alternate = selectedRow as IBirthday;
      }
    });
    if (alternate != null){
      this.menuItems[1].label = `Relate to ${alternate.fname} ${alternate.lname}`;
    } else {
      this.menuItems[1].label = `Select another birthday to relate`;
    }
    this.contextSelectedRow = birthday;
  }

  onMenuShow(menu : any, chips : any): void{
    // this shouldn't be necessary, but the p-menu menuleave is not firing
    const menuEl = menu.el.nativeElement.children[0];
    const chipsEl = chips.el.nativeElement.parentElement;
    let mouseOver : any = null;
    let chipsMouseOut : any = null;
    let bMouseOnMenu = false;
    const hideMenu = ()=>{
      menu.hide();
      chipsEl.removeEventListener('mouseout', chipsMouseOut);
      menuEl.removeEventListener('mouseleave', hideMenu);
      menuEl.removeEventListener('mouseover', mouseOver);
    }
    mouseOver = ()=>{
      bMouseOnMenu = true;
    }
    chipsMouseOut = ()=>{
      setTimeout(function() : void{
        if (!bMouseOnMenu){
          hideMenu();
        }
      }, 0);
    }
    menuEl.addEventListener('mouseover', mouseOver);
    menuEl.addEventListener('mouseleave', hideMenu);
    chipsEl.addEventListener('mouseout', chipsMouseOut);
  }

  onChipClick(event: Event) : Event {
    return event;
  }

  onExpandChange(expanded : boolean) : void {
    if (expanded){
      // ignore
    }
  }

  onRemoveChip(chip : any) : void {
    this.checkboxSelectedRows = this.checkboxSelectedRows.filter((row) => (row as IBirthday).id !== (chip as IBirthday).id);
    this.chipSelectedRows = this.chipSelectedRows.filter((row) => (row as IBirthday).id !== (chip as IBirthday).id);
  }

  isSelected(key : any) : boolean {
    return this.checkboxSelectedRows.some((row) => (row as IBirthday).id === key);
  }

  okCategorize() : void{
    this.bDisplayCategories = false;
  }

  subscribeToSaveResponse(result: Observable<HttpResponse<IBirthday>>): void {
    result.subscribe(
      () => this.onSaveSuccess(),
      () => this.onSaveError()
    );
  }

  protected onSaveSuccess(): void {
    this.refreshData();
  }

  protected onSaveError(): void {
    // Api for inheritance.
  }

  cancelCategorize() : void {
    this.bDisplayCategories = false;
  }

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(data => {
      this.page = data.pagingParams.page;
      this.ascending = data.pagingParams.ascending;
      this.predicate = data.pagingParams.predicate;
      this.ngbPaginationPage = data.pagingParams.page;
      this.loadPage();
    });
    this.registerChangeInBirthdays();
  }

  onCategorySuccess(data: ICategory[] | null, headers: HttpHeaders) : void{
    this.categories = data;
  }

  doMenuView(selectedRow: any) : void {
    this.birthdayDialogTitle = `${selectedRow.fname} ${selectedRow.lname}`;
    this.birthdayDialogId = selectedRow.elasticId;
    this.bDisplayBirthday = true;
  }

  doMenuDelete(selectedRow: any) : void {
    this.delete(selectedRow);
  }

  ngOnDestroy(): void {
    if (this.eventSubscriber) {
      this.eventSubscriber.unsubscribe();
    }
  }

  trackId(index: number, item: IBirthday): number {
    return item.id!;
  }

  sort(): string[] {
    const result = [this.predicate + ',' + (this.ascending ? 'asc' : 'desc')];
    if (this.predicate !== 'id') {
      result.push('id');
    }
    return result;
  }

  sortCategory(): string[] {
    const result = [this.predicate + ',' + (this.ascending ? 'asc' : 'desc')];
    if (this.predicate !== 'id') {
      result.push('id');
    }
    return result;
  }

  protected onSuccess(data: IBirthday[] | null, headers: HttpHeaders, page: number, navigate: boolean): void {
    this.totalItems = Number(headers.get('X-Total-Count'));
    this.page = page;
    if (navigate) {
      this.router.navigate(['/birthday'], {
        queryParams: {
          page: this.page,
          size: this.itemsPerPage,
          sort: this.predicate + ',' + (this.ascending ? 'asc' : 'desc'),
        },
      });
    }
    this.birthdays = data || [];
    this.rowData = of(this.birthdays);
    this.loadingMessage = "";
    this.birthdays.forEach((birthday) => {
      this.birthdaysMap[birthday.id!] = birthday;
    });
  }

  private sortFilter() : void{
    this.loadPage();
  }

  protected onError(): void {
    this.ngbPaginationPage = this.page;
  }

  delete(birthday: IBirthday): void {
    const modalRef = this.modalService.open(BirthdayDeleteDialogComponent, { size: 'lg', backdrop: 'static' });
    modalRef.componentInstance.birthday = birthday;
  }
} 