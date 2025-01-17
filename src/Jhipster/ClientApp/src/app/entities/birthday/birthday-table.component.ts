import { Component, OnInit, OnDestroy, Input, Output, EventEmitter } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { JhiEventManager } from 'ng-jhipster';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

import { IBirthday } from 'app/shared/model/birthday.model';

import { ITEMS_PER_PAGE } from 'app/shared/constants/pagination.constants';
import { BirthdayService } from './birthday.service';
import { CategoryService } from '../category/category.service';
// import { BirthdayDeleteDialogComponent } from './birthday-delete-dialog.component';
import { Observable } from 'rxjs';
import { of } from 'rxjs';
import { SuperTable } from './super-table';
import { MenuItem, MessageService } from 'primeng/api';
import { DomSanitizer } from "@angular/platform-browser";
import { ConfirmationService, PrimeNGConfig} from "primeng/api";
import { faCheck } from '@fortawesome/free-solid-svg-icons';
import { ICategory } from 'app/shared/model/category.model';

@Component({
  selector: 'jhi-birthday-table',
  templateUrl: './birthday-table.component.html',
  providers: [MessageService, ConfirmationService]
})

export class BirthdayTableComponent implements OnInit, OnDestroy {
  birthdays?: IBirthday[];
  birthdaysMap : {} = {};
  eventSubscriber?: Subscription;
  totalItems = 0;
  itemsPerPage = ITEMS_PER_PAGE;
  page!: number;
  predicate!: string;
  ascending!: boolean;
  ngbPaginationPage = 1;
  expandedRows = {};
  faCheck = faCheck;
  @Input() parent: SuperTable | null = null;
  @Input() refresh: any = null;

  columnDefs = [
    { field: 'lname', sortable: true, filter: true },
    { field: 'fname', sortable: true, filter: true },
    { field: 'dob', sortable: true, filter: true/* , valueFormatter: (data: any) => this.formatMediumPipe.transform(dayjs(data.value)) */},
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
    protected eventManager: JhiEventManager,
    protected modalService: NgbModal,
    protected messageService: MessageService,
    public sanitizer:DomSanitizer,
    private confirmationService: ConfirmationService,
    private primeNGConfig : PrimeNGConfig,
  ) {}

  getTableStyle(stdiv: HTMLDivElement):object{

    if (this.parent && this.parent.displayingAsCategories){
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
      // this.rowData = of([]); // trigger showing the 'loading...'
  }

  refreshData(): void {
    this.birthdays =[];
    this.rowData = of(this.birthdays);
    this.loadPage();
  }

  clearFilters(table: SuperTable, searchInput: any): void{
    searchInput.value = ""; // should clear filter
    // table.clear();
    table.reset();
    Object.keys(this.expandedRows).forEach((key)=>{
      this.expandedRows[key] = false;
    });
    // no need to clear checkboxes or chips
    // this.chipSelectedRows = [];
    // this.checkboxSelectedRows = [];
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
    this.menuItems[0].label = `Select action for ${birthday.fname} ${birthday.lname}`;
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
    /*
    const clickTarget : any = event.target;
    const id = clickTarget.children[0].innerHTML;
    this.confirmationService.confirm({
      target: clickTarget,
      message: `Are you sure that you want to proceed with ${id}?`,
      icon: "pi pi-exclamation-triangle",
      accept: () => {
        this.messageService.add({
          severity: "info",
          summary: "Confirmed",
          detail: "You have accepted"
        });
      },
      reject: () => {
        this.messageService.add({
          severity: "error",
          summary: "Rejected",
          detail: "You have rejected"
        });
      }
    });
    */
  }
  onExpandChange(expanded : boolean) : void {
    if (expanded){
      // ignore
    }
    /*
    this.chipSelectedRows = [];
    Object.keys(this.expandedRows).forEach((key)=>{
      if (this.expandedRows[key]){
        this.chipSelectedRows.push(this.birthdaysMap[key]);
      }
    });
    */
  }
  onRemoveChip(chip : any) : void {
    if (this.expandedRows[chip.id]){
      this.expandedRows[chip.id] = false;
    }
    const newSelection : IBirthday[] = [];
    this.checkboxSelectedRows.forEach((row)=>{
      if (row.id !== chip.id){
        newSelection.push(row)
      }
    });
    this.checkboxSelectedRows = newSelection;
  }
  isSelected(key : any) : boolean {
    let ret = false;
    this.checkboxSelectedRows.forEach((row)=>{
      if (row.id === key){
        ret = true;
      }
    });
    return ret;
  }
  okCategorize() : void{
    if (this.selectedCategories.join(",") !== this.initialSelectedCategories){
      (this.contextSelectedRow as IBirthday).categories = this.selectedCategories;
      this.subscribeToSaveResponse(this.birthdayService.update(this.contextSelectedRow as IBirthday));
    }
    this.bDisplayCategories = false;
  }
  subscribeToSaveResponse(result: Observable<HttpResponse<IBirthday>>): void {
    const refresh: any = this.refresh;
    result.subscribe(
      () => {
        this.bDisplayCategories = false;
        if (this.refresh != null){
          setTimeout(()=>{
            refresh();
          },1500); // seems to require some time for elastic to catch up
        }
      },
      () => {
        // how to provide error
        this.bDisplayCategories = false;
      }
    );
  }
  cancelCategorize() : void {
    this.bDisplayCategories = false;
  }
  ngOnInit(): void {
    this.loadPage(1, true);
    this.registerChangeInBirthdays();
    this.primeNGConfig.ripple = true;
    this.menuItems = [{
      label: 'Options',
      items: [
        {
          label: 'Focus',
          icon: 'pi pi-angle-double-down',
          command: ()=>{
            setTimeout(()=>{
              this.setViewFocus.emit(this.contextSelectedRow as IBirthday); // used to provide controller a reference to the table
            }, 0);
          }
        }
        ,
        {
          label: 'Categorize',
          icon: 'pi pi-bookmark',
          command: ()=>{
            setTimeout(()=>{
              this.selectedCategories = [];
              const selectedRow = this.contextSelectedRow;
              this.birthdayDialogId = selectedRow ? selectedRow?.id?.toString() : "";
              this.birthdayDialogTitle = selectedRow ? selectedRow?.fname + " " + selectedRow?.lname : "";
              this.categoryService
              .postQuery({
                page: 0,
                size: 10000,
                sort: this.sortCategory(),
                query: this.birthdayDialogId
              })
              .subscribe(
                (res: any) => this.onCategorySuccess(res.body, res.headers),
                () => this.onError()
              );
            }, 0);
          }
        },
        {
          label: 'Display',
          icon: 'pi pi-book',
          command: ()=>{
            setTimeout(()=>{
              this.birthdayDialogId = this.contextSelectedRow ? this.contextSelectedRow?.id?.toString() : "";
              this.birthdayDialogTitle = this.contextSelectedRow ? this.contextSelectedRow?.lname as string : "";
              this.bDisplayBirthday = true;
            }, 0);
          },
        },
        {
            label: 'Ingest',
            icon: 'pi pi-upload',
        },

      ]},
      {
          label: 'Relationship',
          items: [{
              label: 'Favorable',
              icon: 'pi pi-thumbs-up'
          },
          {
              label: 'Unfavorable',
              icon: 'pi pi-thumbs-down'
          },
          {
              label: 'Iden',
              icon: 'pi pi-id-card'
          },
          {
              label: 'Revision',
              icon: 'pi pi-pencil'
          }
      ]}
    ];
  }
  onCategorySuccess(data: ICategory[] | null, headers: HttpHeaders) : void{
    const totalItems = Number(headers.get('X-Total-Count'));
    this.selectedCategories = [];
    this.categories = [];
    const selectedCategoryNames:string[] = [];
    if (totalItems > 0 || (data && data?.length > 0)){
      data?.forEach(r=>{
        this.categories.push(r);
        if (r.selected){
          this.selectedCategories.push(r);
          selectedCategoryNames.push(r.categoryName as string);
        }
      });
    }
    this.initialSelectedCategories = selectedCategoryNames.join(",");
    this.bDisplayCategories = true;
  }
  doMenuView(selectedRow: any) : void {
    const selected : IBirthday = selectedRow;
    // const count = this.checkboxSelectedRows.length;
    this.messageService.add({severity: 'success', summary: 'Row Viewed', detail: selected.lname });
  }

  doMenuDelete(selectedRow: any) : void {
    const selectedRowType : string = selectedRow.constructor.name;
    this.messageService.add({severity: 'success', summary: 'Row Deleted', detail: selectedRowType});
  }

  ngOnDestroy(): void {
    if (this.eventSubscriber) {
      this.eventManager.destroy(this.eventSubscriber);
    }
  }

  trackId(index: number, item: IBirthday): number {
    // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-assertion
    return item.id!;
  }

  registerChangeInBirthdays(): void {
    this.eventSubscriber = this.eventManager.subscribe('birthdayListModification', () => this.loadPage());
  }

  /* delete(birthday: IBirthday): void {
    const modalRef = this.modalService.open(BirthdayDeleteDialogComponent, { size: 'lg', backdrop: 'static' });
    modalRef.componentInstance.birthday = birthday;
  }*/

  sort(): string[] {
    const result = [this.predicate + ',' + (this.ascending ? 'asc' : 'desc')];
    if (this.predicate !== 'id') {
      result.push('id');
    }
    return result;
  }

  sortCategory(): string[] {
    const result = ['categoryName,asc'];
    result.push('id');
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
    // uncomment next to simulate more data
    // this.birthdays = [...this.birthdays,...this.birthdays,...this.birthdays,...this.birthdays,...this.birthdays,...this.birthdays];
    this.birthdays.forEach((birthday)=>{
      this.birthdaysMap[birthday.id as number] = birthday;
    });
    this.ngbPaginationPage = this.page;

    if (data) {
      const loadIncrement = 250;
      const loadData : any[] = this.birthdays?.slice(0, loadIncrement);
      let loaded = loadIncrement;
      const rowLoader = ()=>{
        loadData.splice(loaded, 0, ...(this.birthdays as any[]).slice(loaded, loaded + loadIncrement));
        loaded += loadIncrement;
        const limitData = 3000;
        if (loadData.length > limitData){
          this.loadingMessage = this.birthdays?.length + " hits (too many to display, showing the first " + limitData +")";
          loadData.length = limitData;
          this.rowData = of(loadData);
          this.sortFilter();
          return;
        }
        this.rowData = of(loadData);
        if (loaded < (this.birthdays as any[]).length){
          this.loadingMessage = "Loading " + loaded + "..."
          setTimeout(rowLoader, 10);
        } else {
          this.loadingMessage = "";
          this.sortFilter();
        }
      }
      this.rowData = of(this.birthdays.slice(0,loadIncrement));
      setTimeout(rowLoader, 10);
    }
    // this.sortFilter();
  }

  private sortFilter() : void{
    if (this.parent){
      const parent = this.parent;
      parent.doFilter();
      parent.sortSingle();
      setTimeout(function() : void{
        parent.setChildWidths();
      }, 0);
    }
  }

  protected onError(): void {
    this.ngbPaginationPage = this.page ?? 1;
  }
}