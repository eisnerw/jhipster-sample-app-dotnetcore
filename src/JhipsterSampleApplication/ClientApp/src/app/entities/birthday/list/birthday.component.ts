/* eslint-disable */ 

import { Component, OnInit, ViewChild, ViewChildren, QueryList, ElementRef, TemplateRef } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { combineLatest, BehaviorSubject, Subscription, Observable, of } from 'rxjs';
import { NgbModal, NgbPagination } from '@ng-bootstrap/ng-bootstrap';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import ItemCountComponent from 'app/shared/pagination/item-count.component';
import { NgbPaginationModule } from '@ng-bootstrap/ng-bootstrap';
import { MenuItem, MessageService } from 'primeng/api';
import { DomSanitizer } from '@angular/platform-browser';
import { ConfirmationService } from 'primeng/api';
import { ContextMenu, ContextMenuModule } from 'primeng/contextmenu';
import { Table } from 'primeng/table';
import { Menu } from 'primeng/menu';
import { TableModule } from 'primeng/table';
import { tap, switchMap, map } from 'rxjs/operators';

import { BirthdayService, EntityArrayResponseType } from '../service/birthday.service';
import { IBirthday } from '../birthday.model';
import { BirthdayDeleteDialogComponent } from '../delete/birthday-delete-dialog.component';
import { SortDirective, SortByDirective } from 'app/shared/sort';
import SharedModule from 'app/shared/shared.module';
import {
  SuperTable,
  ColumnConfig,
} from '../../../shared/SuperTable/super-table.component';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';
import { BirthdayGroupDetailComponent } from './birthday-group-detail.component';
import { TableColResizeEvent, TableColumnReorderEvent } from 'primeng/table';

import { ITEMS_PER_PAGE, PAGE_HEADER, TOTAL_COUNT_RESPONSE_HEADER } from 'app/config/pagination.constants';
import { ASC, DESC, SORT, ITEM_DELETED_EVENT, DEFAULT_SORT_DATA } from 'app/config/navigation.constants';

@Component({
  selector: 'jhi-birthday',
  templateUrl: './birthday.component.html',
  styleUrls: ['./birthday.component.scss'],
  schemas: [NO_ERRORS_SCHEMA],
  providers: [MessageService, ConfirmationService],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    SharedModule,
    SuperTable,
    TableModule,
    BirthdayGroupDetailComponent,
  ],
  standalone: true,
})
export class BirthdayComponent implements OnInit {
  @ViewChild('contextMenu') contextMenu!: ContextMenu;
  @ViewChild('superTable') superTable!: SuperTable;
  @ViewChild('searchInput') searchInput!: ElementRef<HTMLInputElement>;
  @ViewChild('menu') menu!: Menu;
  @ViewChild('expandedRow', { static: true }) expandedRowTemplate: TemplateRef<any> | undefined;
  @ViewChild('headerTable') headerTable!: SuperTable;
  @ViewChildren(BirthdayGroupDetailComponent) groupDetailComponents!: QueryList<BirthdayGroupDetailComponent>;

  dataLoader: DataLoader<IBirthday>;
  headerDataLoader: DataLoader<IBirthday>;
  groups$: Observable<string[]>;
  itemsPerPage = 50;
  page = 1;
  predicate!: string;
  ascending!: boolean;
  ngbPaginationPage = 1;
  columns: ColumnConfig[] = [
    {
      field: 'lineNumber',
      header: '#',
      type: 'lineNumber',
      width: '4rem',
    },
    {
      field: 'checkbox',
      header: '',
      type: 'checkbox',
      width: '2rem',
    },
    {
      field: 'lname',
      header: 'Name',
      filterType: 'text',
      width: '200px',
      type: 'string',
    },
    {
      field: 'fname',
      header: 'First',
      filterType: 'text',
      type: 'string',
    },
    {
      field: 'dob',
      header: 'Date of Birth',
      filterType: 'date',
      type: 'date',
      dateFormat: 'MM/dd/yyyy',
    },
    {
      field: 'sign',
      header: 'Sign',
      type: 'list',
      listOptions: [
        { label: 'Aries', value: 'Aries' },
        { label: 'Taurus', value: 'Taurus' },
        { label: 'Gemini', value: 'Gemini' },
        { label: 'Cancer', value: 'Cancer' },
        { label: 'Leo', value: 'Leo' },
        { label: 'Virgo', value: 'Virgo' },
        { label: 'Libra', value: 'Libra' },
        { label: 'Scorpio', value: 'Scorpio' },
        { label: 'Sagittarius', value: 'Sagittarius' },
        { label: 'Capricorn', value: 'Capricorn' },
        { label: 'Aquarius', value: 'Aquarius' },
        { label: 'Pisces', value: 'Pisces' }
      ]
    },
    {
      field: 'isAlive',
      header: 'Alive?',
      filterType: 'boolean',
      type: 'boolean',
    },
    {
      field: 'expander',
      header: '',
      type: 'expander',
      width: '25px',
      style: 'font-weight: 700;',
    },
  ];

  expandedRowKeys: { [key: string]: boolean } = {};

  onRowExpand(event: { originalEvent: Event; data: any }): void {
    this.expandedRowKeys[event.data] = true;
  }

  onRowCollapse(event: any): void {
    delete this.expandedRowKeys[event.data];
  }

  onGroupToggle(groupName: string): void {
    this.expandedRowKeys[groupName] = !this.expandedRowKeys[groupName];
  }

  isGroupExpanded(groupName: string): boolean {
    return this.expandedRowKeys[groupName] === true;
  }

  // New properties for super-table
  menuItems: MenuItem[] = [
    {
      label: 'Select action',
      items: [
        {
          label: 'View',
          icon: 'pi pi-fw pi-search',
          command() {
            // Handle view action
          },
        },
        {
          label: 'Edit',
          icon: 'pi pi-fw pi-pencil',
          command() {
            // Handle edit action
          },
        },
        {
          label: 'Delete',
          icon: 'pi pi-fw pi-trash',
          command() {
            // Handle delete action
          },
        },
      ],
    },
    {
      label: 'Select another birthday to relate',
      items: [
        {
          label: 'Relate',
          icon: 'pi pi-fw pi-link',
          command() {
            // Handle relate action
          },
        },
      ],
    },
  ];
  contextSelectedRow: IBirthday | null = null;
  checkboxSelectedRows: IBirthday[] = [];
  chipSelectedRows: IBirthday[] = [];

  bDisplaySearchDialog = false;
  bDisplayBirthday = false;
  birthdayDialogTitle = '';
  birthdayDialogId: any = '';
  viewMode: 'grid' | 'group' = 'grid';
  private loadingSubscription?: Subscription;

  constructor(
    protected birthdayService: BirthdayService,
    protected activatedRoute: ActivatedRoute,
    protected router: Router,
    protected modalService: NgbModal,
    protected messageService: MessageService,
    public sanitizer: DomSanitizer,
    private confirmationService: ConfirmationService
  ) {
    const fetchFunction: FetchFunction<IBirthday> = (queryParams: any) => {
      return this.birthdayService.query(queryParams);
    };
    this.dataLoader = new DataLoader<IBirthday>(fetchFunction);

    const dummyFetch: FetchFunction<IBirthday> = () => of(new HttpResponse<any>({ body: { hits: [], totalHits: 0, searchAfter: [], pitId: null } }));
    this.headerDataLoader = new DataLoader<IBirthday>(dummyFetch);

    this.groups$ = this.birthdayService.getUniqueValues('fname').pipe(
      map(response => response.body ?? []),
      map(names => names.sort())
    );
  }

  ngOnInit(): void {
    this.handleNavigation();
  }

  trackId(index: number, item: IBirthday): string {
    return item.id!;
  }

  showSearchDialog(): void {
    this.bDisplaySearchDialog = true;
  }

  cancelSearchDialog(): void {
    this.bDisplaySearchDialog = false;
  }

  refreshData(): void {
    this.loadPage();
  }

  clearFilters(table: any, searchInput: HTMLInputElement): void {
    searchInput.value = '';
    table.reset();
    /*
    Object.keys(this.expandedRows).forEach((key) => {
      this.expandedRows[key] = false;
    });
    */
    table.filterGlobal(searchInput.value, 'contains');
  }

  onExpandChange(expanded: boolean): void {
    // Handle row expansion
  }

  onCheckboxChange(): void {
    this.chipSelectedRows = [];
    if (this.checkboxSelectedRows.length < 3) {
      this.checkboxSelectedRows.forEach((row) => {
        this.chipSelectedRows.push(row);
      });
    }
  }

  setMenu(birthday: IBirthday | null): void {
    if (birthday != null && this.menuItems[0]?.items) {
      const firstName = birthday.fname ?? '';
      const lastName = birthday.lname ?? '';
      this.menuItems[0].label = `Select action for ${firstName} ${lastName}`;
    }
    let alternate: IBirthday | null = null;
    for (const selectedRow of this.chipSelectedRows) {
      if (birthday != null && selectedRow.id !== birthday.id) {
        alternate = selectedRow;
        break;
      }
    }
    if (alternate) {
      const firstName = alternate.fname ?? '';
      const lastName = alternate.lname ?? '';
      this.menuItems[1].label = `Relate to ${firstName} ${lastName}`;
    } else {
      this.menuItems[1].label = `Select another birthday to relate`;
    }
    this.contextSelectedRow = birthday;
  }

  showMenu(event: MouseEvent): void {
    this.menu.show(event);
  }

  onMenuShow(event: any, chips: any): void {
    const menuEl = event.target;
    const chipsEl = chips.el.nativeElement.parentElement;
    let mouseOver: any = null;
    let chipsMouseOut: any = null;
    let bMouseOnMenu = false;
    const hideMenu = (): void => {
      this.menu.hide();
      chipsEl.removeEventListener('mouseout', chipsMouseOut);
      menuEl.removeEventListener('mouseleave', hideMenu);
      menuEl.removeEventListener('mouseover', mouseOver);
    };
    mouseOver = (): void => {
      bMouseOnMenu = true;
    };
    chipsMouseOut = (): void => {
      setTimeout(function (): void {
        if (!bMouseOnMenu) {
          hideMenu();
        }
      }, 0);
    };
    menuEl.addEventListener('mouseover', mouseOver);
    menuEl.addEventListener('mouseleave', hideMenu);
    chipsEl.addEventListener('mouseout', chipsMouseOut);
  }

  onChipClick(event: any): void {
  }

  onContextMenuSelect(data: any): void {
    this.contextSelectedRow = data;
  }

  onContextMenuMouseLeave(): void {
    this.contextMenu.hide();
  }

  onRemoveChip(event: any): void {
    this.chipSelectedRows = this.chipSelectedRows.filter(row => row.id !== event.id);
  }

  delete(birthday: IBirthday): void {
    const modalRef = this.modalService.open(BirthdayDeleteDialogComponent, { size: 'lg', backdrop: 'static' });
    modalRef.componentInstance.birthday = birthday;
    // unsubscribe not needed because closed completes on modal close
    modalRef.closed.subscribe(reason => {
      if (reason === ITEM_DELETED_EVENT) {
        this.loadPage();
      }
    });
  }

  loadPage(): void {
    this.dataLoader.load(this.itemsPerPage, this.predicate, this.ascending, { luceneQuery: '*' });
  }

  protected sort(): string[] {
    const result = [this.predicate + ',' + (this.ascending ? ASC : DESC)];
    if (this.predicate !== 'id') {
      result.push('id');
    }
    return result;
  }

  protected handleNavigation(): void {
    combineLatest([this.activatedRoute.data, this.activatedRoute.queryParamMap])
      .subscribe(([data, params]) => {
        const page = params.get('page');
        const pageSize = params.get('size');
        this.page = page !== null ? +page : 1;
        this.itemsPerPage = pageSize !== null ? +pageSize : this.itemsPerPage;
        const sort = (params.get(SORT) ?? data[DEFAULT_SORT_DATA]).split(',');
        this.predicate = sort[0];
        this.ascending = sort[1] === ASC;
        this.ngbPaginationPage = this.page;
        this.dataLoader.load(this.itemsPerPage, this.predicate, this.ascending, { luceneQuery: '*' });
      });
  }

  logSort(event: any): void {
    console.log('sort event', event);
  }

  onHeaderSort(event: any): void {
    this.groupDetailComponents?.forEach(cmp => cmp.applySort(event));
  }

  onHeaderFilter(event: any): void {
    this.groupDetailComponents?.forEach(cmp => cmp.applyFilter(event));
  }

  onHeaderColResize(event: TableColResizeEvent): void {
    if (event?.element) {
      const index = (event.element as any).cellIndex;
      const newWidthPx = event.element.offsetWidth + 'px';
      const newWidth = event.element.offsetWidth;
      if (this.columns[index]) {
        this.columns[index].width = newWidthPx;
        this.columns = [...this.columns];
      }

      // Update style of header table immediately
      const headerWidths = this.columns.map(c => parseInt(c.width || '0', 10));
      (this.headerTable.pTable as any).updateStyleElement(headerWidths, index, newWidth, null);
    }

    // Propagate updated column definitions to child tables via Input binding
    this.groupDetailComponents?.forEach(cmp => {
      cmp.columns = [...this.columns];
      const childWidths = cmp.columns.map(c => parseInt(c.width || '0', 10));
      (cmp.superTableComponent.pTable as any).updateStyleElement(childWidths, index, newWidth, null);
    });
  }

  onColumnReorder(event: TableColumnReorderEvent): void {
    if (event.dragIndex == null || event.dropIndex == null) {
      return;
    }

    const cols = [...this.columns];
    const moved = cols.splice(event.dragIndex, 1)[0];
    cols.splice(event.dropIndex, 0, moved);
    this.columns = cols;

    this.groupDetailComponents?.forEach(cmp => {
      cmp.columns = [...this.columns];
    });
  }
}
