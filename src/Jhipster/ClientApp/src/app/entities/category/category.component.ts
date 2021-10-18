import { Component, OnInit, OnDestroy } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { ActivatedRoute, ParamMap, Router, Data } from '@angular/router';
import { Subscription, combineLatest } from 'rxjs';
import { JhiEventManager } from 'ng-jhipster';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

import { IBirthday } from 'app/shared/model/birthday.model';

import { ICategory } from 'app/shared/model/category.model';

import { ITEMS_PER_PAGE } from 'app/shared/constants/pagination.constants';
import { BirthdayService } from '../birthday/birthday.service';
import { CategoryService } from './category.service';

// import { CategoryDeleteDialogComponent } from './category-delete-dialog.component';
import { Observable } from 'rxjs';
import { of } from 'rxjs';
import { Table } from 'primeng/table';
import { MenuItem, MessageService } from 'primeng/api';
import { DomSanitizer } from "@angular/platform-browser";
import { ConfirmationService, PrimeNGConfig} from "primeng/api";
import { faCheck } from '@fortawesome/free-solid-svg-icons';


@Component({
  selector: 'jhi-category',
  templateUrl: './category.component.html',
  providers: [MessageService, ConfirmationService]
})

export class CategoryComponent implements OnInit, OnDestroy {
  categories?: ICategory[];
  
  selectableCategories: ICategory[] = [];
  
  categoriesMap : {} = {};
  eventSubscriber?: Subscription;
  totalItems = 0;
  itemsPerPage = ITEMS_PER_PAGE;
  page!: number;
  predicate!: string;
  ascending!: boolean;
  ngbPaginationPage = 1;
  expandedRows = {};
  loading = true;
  faCheck = faCheck;
  
  columnDefs = [
    { field: 'categoryName', sortable: true, filter: true },
  ];

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

  databaseQuery = "";

  refresh:any = null;

  selectedCategories : ICategory[] = [];

  initialSelectedCategories = "";

  constructor(
    protected categoryService: CategoryService,
    protected birthdayService: BirthdayService,
    protected activatedRoute: ActivatedRoute,
    protected router: Router,
    protected eventManager: JhiEventManager,
    protected modalService: NgbModal,
    protected messageService: MessageService,
    public sanitizer:DomSanitizer,
    private primeNGConfig : PrimeNGConfig,
  ) {
    this.selectableCategories = []; // For some reason, selectableCategories is not visible in the html without this
    this.refresh = this.refreshData.bind(this);
  }

  loadPage(page?: number, dontNavigate?: boolean): void {
    const pageToLoad: number = page || this.page || 1;

    this.loading = true;
    this.categoryService
      .query({
        page: pageToLoad - 1,
        size: this.itemsPerPage,
        sort: this.sort(),
        query: this.databaseQuery
      })
      .subscribe(
        (res: HttpResponse<ICategory[]>) => this.onSuccess(res.body, res.headers, pageToLoad, !dontNavigate),
        () => this.onError()
      );
  }

  refreshData(): void {
    this.categories =[];
    this.selectableCategories =[];
    this.rowData = of(this.categories);
    this.loadPage();
  }

  clearFilters(table: Table, searchInput: any): void{
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
  showSearchDialog() : void {
    // initialize dialog here
    this.bDisplaySearchDialog = true;
  }

  cancelSearchDialog() : void {
    this.bDisplaySearchDialog = false;
  }

  okSearchDialog(queryBuilder : any) : void {
    if (queryBuilder.query.rules && queryBuilder.query.rules.length === 0){
      this.databaseQuery = "";
    } else {
      this.databaseQuery = JSON.stringify(queryBuilder.query);
    }
    this.bDisplaySearchDialog = false;
    this.refreshData();
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
  }
  onExpandChange(expanded : boolean) : void {
    if (expanded){
      // ignore
    }
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
  addToSelectedCategories(categoryInput : any) : void {
    let category = categoryInput;
    let categoryPresent = false;
    (this.categories as any[]).forEach(c=>{
      if (c.categoryName === category.categoryName){
        categoryPresent = true;
        category = c;
      }
    });
    if (!categoryPresent){
      this.categories?.push(category);
    }
    let selectedCategoryPresent = false;
    this.selectedCategories.forEach(c=>{
      if (c.categoryName === category.categoryName){
        selectedCategoryPresent = true;
      }
    });
    if (!selectedCategoryPresent){
      this.selectedCategories.push(category);
    } 
  }
  ngOnInit(): void {
    this.handleNavigation();
    this.registerChangeInCategories();
    this.primeNGConfig.ripple = true;
    this.menuItems = [{
      label: 'Options',
      items: [
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
              .query({
                page: 0,
                size: 10000,
                sort: this.sortCategory(),
                query: this.birthdayDialogId
              })
              .subscribe(
                (res: HttpResponse<IBirthday[]>) => this.onCategorySuccess(res.body, res.headers),
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
        }]
      }
    ];
  }
  onCategorySuccess(data: ICategory[] | null, headers: HttpHeaders) : void{
    const totalItems = Number(headers.get('X-Total-Count'));
    this.selectedCategories = [];
    if (totalItems > 0 || (data && data?.length > 0)){
      data?.forEach(r=>{
          this.selectableCategories.forEach(p=>{
            if (p.categoryName === r.categoryName){
              this.selectedCategories.push(p);
            }
          });
      });
    }
    this.initialSelectedCategories = this.selectedCategories.join(",");
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

  protected handleNavigation(): void {
    combineLatest(this.activatedRoute.data, this.activatedRoute.queryParamMap, (data: Data, params: ParamMap) => {
      const page = params.get('page');
      const pageNumber = page !== null ? +page : 1;
      /*
      const sort = (params.get('sort') ?? data['defaultSort']).split(',');
      const predicate = sort[0];
      const ascending = sort[1] === 'asc';
      if (pageNumber !== this.page || predicate !== this.predicate || ascending !== this.ascending) {
        this.predicate = predicate;
        this.ascending = ascending;
      */
        this.loadPage(pageNumber, true);
      // }
    }).subscribe();
  }
  ngOnDestroy(): void {
    if (this.eventSubscriber) {
      this.eventManager.destroy(this.eventSubscriber);
    }
  }

  trackId(index: number, item: ICategory): number {
    // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-assertion
    return item.id!;
  }

  registerChangeInCategories(): void {
    this.eventSubscriber = this.eventManager.subscribe('categoryListModification', () => this.loadPage());
  }

  /* delete(category: ICategory): void {
    const modalRef = this.modalService.open(CategoryDeleteDialogComponent, { size: 'lg', backdrop: 'static' });
    modalRef.componentInstance.category = category;
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


  protected onSuccess(data: ICategory[] | null, headers: HttpHeaders, page: number, navigate: boolean): void {
    this.totalItems = Number(headers.get('X-Total-Count'));
    this.page = page;
    if (navigate) {
      this.router.navigate(['/category'], {
        queryParams: {
          page: this.page,
          size: this.itemsPerPage,
          sort: this.predicate + ',' + (this.ascending ? 'asc' : 'desc'),
        },
      });
    }
    this.categories = data || [];
    this.categories.forEach((category)=>{
      this.categoriesMap[category.id as number] = category;
      if (!category.notCategorized){
        this.selectableCategories?.push(category);
      }
    });
    this.ngbPaginationPage = this.page;
    
    if (data) {
      this.rowData = of(this.categories);
    }
    this.loading = false;
  }

  protected onError(): void {
    this.ngbPaginationPage = this.page ?? 1;
  }
}
