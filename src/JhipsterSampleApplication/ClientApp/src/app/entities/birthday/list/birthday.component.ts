/* eslint-disable */ 

import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { combineLatest, BehaviorSubject, Subscription } from 'rxjs';
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
import { DatePipe } from '@angular/common';
import { TableModule } from 'primeng/table';

import { BirthdayService, EntityArrayResponseType } from '../service/birthday.service';
import { IBirthday } from '../birthday.model';
import { BirthdayDeleteDialogComponent } from '../delete/birthday-delete-dialog.component';
import { SortDirective, SortByDirective } from 'app/shared/sort';
import SharedModule from 'app/shared/shared.module';
import {
  SuperTable,
  ColumnConfig,
} from '../../../shared/SuperTable/super-table';

@Component({
  selector: 'jhi-birthday',
  templateUrl: './birthday.component.html',
  schemas: [NO_ERRORS_SCHEMA],
  providers: [MessageService, ConfirmationService],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    SharedModule,
    DatePipe,
    SuperTable,
    TableModule,
  ],
  standalone: true,
})
export class BirthdayComponent implements OnInit {
  @ViewChild('contextMenu') contextMenu!: ContextMenu;
  @ViewChild('superTable') superTable!: SuperTable;
  @ViewChild('searchInput') searchInput!: ElementRef<HTMLInputElement>;
  @ViewChild('menu') menu!: Menu;

  birthdays: IBirthday[] = [];
  isLoading = false;
  loadingMessage = '';
  loadingMessage$ = new BehaviorSubject<string>('');
  totalItems = 0;
  itemsPerPage = 50;
  page?: number;
  predicate!: string;
  ascending!: boolean;
  ngbPaginationPage = 1;
  superTableParent = 'superTableParent';
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

  onRowExpand(event: { originalEvent: Event, data: any }) {
    console.log('Expanded:', event.data);
    this.expandedRowKeys[event.data.id] = true;
  }

  onRowCollapse(event: any) {
    delete this.expandedRowKeys[event.data.id];
  }


  // New properties for super-table
  rowData = new BehaviorSubject<IBirthday[]>([]);
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
  loading = false;
  presidents = [
    'Adams', 'Arthur', 'Biden', 'Buchanan', 'Bush', 'Carter', 'Cleveland', 'Clinton', 'Coolidge',
    'Eisenhower', 'Fillmore', 'Ford', 'Garfield', 'Grant', 'Harding', 'Harrison', 'Hayes',
    'Hoover', 'Jackson', 'Jefferson', 'Johnson', 'Kennedy', 'Lincoln', 'Madison', 'McKinley',
    'Monroe', 'Nixon', 'Obama', 'Pierce', 'Polk', 'Reagan', 'Roosevelt', 'Taft', 'Taylor',
    'Truman', 'Trump', 'Tyler', 'Van Buren', 'Washington', 'Wilson'
  ].sort();
  viewMode: 'grid' | 'group' = 'group';
  private loadingSubscription?: Subscription;

  constructor(
    protected birthdayService: BirthdayService,
    protected activatedRoute: ActivatedRoute,
    protected router: Router,
    protected modalService: NgbModal,
    protected messageService: MessageService,
    public sanitizer: DomSanitizer,
    private confirmationService: ConfirmationService,
  ) {}

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
    // Handle chip click event
  }

  onContextMenuMouseLeave(): void {
    this.contextMenu.hide();
  }

  onRemoveChip(event: any): void {
    const chip = event.value;
    this.checkboxSelectedRows = this.checkboxSelectedRows.filter(
      (row) => row.id !== chip.id,
    );
    this.chipSelectedRows = this.chipSelectedRows.filter(
      (row) => row.id !== chip.id,
    );
  }

  delete(birthday: IBirthday): void {
    const modalRef = this.modalService.open(BirthdayDeleteDialogComponent, {
      size: 'lg',
      backdrop: 'static',
    });
    modalRef.componentInstance.birthday = birthday;
    modalRef.closed.subscribe((reason: string) => {
      if (reason === 'deleted') {
        this.loadPage();
      }
    });
  }

  loadPage(page?: number, dontNavigate?: boolean): void {
    this.isLoading = true;
    const pageToLoad: number = page ?? this.page ?? 1;

    this.birthdayService
      .query({
        page: pageToLoad - 1,
        pageSize: this.itemsPerPage,
        sort: this.sort(),
      })
      .subscribe({
        next: (res: EntityArrayResponseType) => {
          this.isLoading = false;
          this.onSuccess(res.body, res.headers, pageToLoad, !dontNavigate);
        },
        error: () => {
          this.isLoading = false;
          this.onError();
        },
      });
  }

  protected sort(): string[] {
    const result = [this.predicate + ',' + (this.ascending ? 'asc' : 'desc')];
    if (this.predicate !== 'id') {
      result.push('id');
    }
    return result;
  }

  protected handleNavigation(): void {
    combineLatest([
      this.activatedRoute.data,
      this.activatedRoute.queryParamMap,
    ]).subscribe(([data, params]) => {
      const page = params.get('page');
      const pageNumber = page !== null ? +page : 1;
      const sort = (params.get('sort') ?? data['defaultSort']).split(',');
      const predicate = sort[0];
      const ascending = sort[1] === 'asc';
      if (
        pageNumber !== this.page ||
        predicate !== this.predicate ||
        ascending !== this.ascending
      ) {
        this.predicate = predicate;
        this.ascending = ascending;
        this.loadPage(pageNumber, true);
      }
    });
  }

  protected onSuccess(
    data: {
      hits: IBirthday[];
      hitType: string;
      totalHits: number;
      searchAfter: string[];
      pitId: string | null;
    } | null,
    headers: HttpHeaders,
    page: number,
    navigate: boolean,
  ): void {
    this.totalItems = data?.totalHits ?? Number(headers.get('X-Total-Count'));
    this.page = page;
    this.birthdays = data?.hits ?? [];
    this.rowData.next(this.birthdays);
    const limitData = 1000;

    if (this.birthdays && this.birthdays.length < this.totalItems && this.birthdays.length < limitData) {
      const loadIncrement = 50;
      let loaded = this.birthdays.length;
      let currentPitId = data?.pitId;
      let currentSearchAfter = data?.searchAfter;
      let chunkCounter = 0;

      const rowLoader = () => {
        if (!currentPitId || !currentSearchAfter) {
          this.loadingMessage$.next('');
          return;
        }

        this.birthdayService
          .query({
            page: 0,
            pageSize: loadIncrement,
            sort: this.sort(),
            pitId: currentPitId,
            searchAfter: currentSearchAfter,
          })
          .subscribe({
            next: (res: EntityArrayResponseType) => {
              if (res.body?.hits) {
                this.birthdays.push(...res.body.hits);
                loaded = this.birthdays.length;
                currentPitId = res.body.pitId;
                currentSearchAfter = res.body.searchAfter;
                
                if (loaded >= limitData) {
                  this.loadingMessage = `${this.totalItems} hits (too many to display, showing the first ${limitData})`;
                  this.birthdays = this.birthdays.slice(0, limitData);
                  this.rowData.next(this.birthdays);
                  this.loadingMessage$.next(this.loadingMessage);
                  return;
                }

                chunkCounter++;
                if (loaded < this.totalItems) {
                  this.loadingMessage = `loading ${loaded}...`;
                  this.loadingMessage$.next(this.loadingMessage);
                  if (chunkCounter % 5 === 0) {
                    this.rowData.next(this.birthdays);
                  }
                  setTimeout(rowLoader, 10);
                } else {
                  this.loadingMessage$.next('');
                  this.rowData.next(this.birthdays);
                }
              }
            },
            error: () => {
              this.loadingMessage$.next('Error loading data.');
              this.onError();
            },
          });
      };

      if (this.birthdays.length < this.totalItems && this.birthdays.length < limitData) {
        this.loadingMessage$.next(`loading ${this.birthdays.length}...`);
        setTimeout(rowLoader, 10);
      }
    }

    if (navigate) {
      this.router.navigate(['/birthday'], {
        queryParams: {
          page: this.page,
          sort: this.sort().join(','),
        },
      });
    }
  }

  protected onError(): void {
    this.ngbPaginationPage = this.page ?? 1;
  }

  logSort(event: any): void {
    const { data, field, order } = event;
    console.log(`--- Sorting ---`);
    console.log(`Field: ${field}, Order: ${order === 1 ? 'asc' : 'desc'}`);
    console.log(`Component 'birthdays' length: ${this.birthdays?.length}`);
    console.log(`Event 'data' length: ${data.length}`);
    
    // Check for duplicates in the component's master list
    const masterIds = new Set(this.birthdays?.map(b => b.id));
    if (masterIds.size !== this.birthdays?.length) {
      console.error(`[ERROR] Duplicate IDs found in component's master 'birthdays' list BEFORE sort.`);
    }

    // Check for duplicates in the array sorted by PrimeNG
    const eventIds = new Set(data.map((b: IBirthday) => b.id));
    if (eventIds.size !== data.length) {
      console.error(`[ERROR] Duplicate IDs found in the 'event.data' array AFTER sort.`);
    }
    console.log(`--- End Sorting ---`);
  }
}
