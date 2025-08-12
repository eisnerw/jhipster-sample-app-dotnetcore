/* eslint-disable */

import { Component, OnInit, ViewChild, ElementRef, TemplateRef, AfterViewInit, inject } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { combineLatest, Subscription } from 'rxjs';
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
import { InputTextModule } from 'primeng/inputtext';
import { tap, switchMap, map } from 'rxjs/operators';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';

import {
  BirthdayService,
  EntityArrayResponseType,
} from '../service/birthday.service';
import { IBirthday } from '../birthday.model';
import { ViewService } from '../../view/service/view.service';
import { BirthdayDeleteDialogComponent } from '../delete/birthday-delete-dialog.component';
import { SortDirective, SortByDirective } from 'app/shared/sort';
import SharedModule from 'app/shared/shared.module';
import {
  SuperTable,
  ColumnConfig,
  GroupDescriptor,
  GroupData,
} from '../../../shared/SuperTable/super-table.component';
import { DataLoader, FetchFunction } from 'app/shared/data-loader';

import {
  ITEMS_PER_PAGE,
  PAGE_HEADER,
  TOTAL_COUNT_RESPONSE_HEADER,
} from 'app/config/pagination.constants';
import {
  ASC,
  DESC,
  SORT,
  ITEM_DELETED_EVENT,
  DEFAULT_SORT_DATA,
} from 'app/config/navigation.constants';
import {
  QueryInputComponent,
  bqlToRuleset,
  rulesetToBql,
} from 'popup-ngx-query-builder';

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
    QueryInputComponent,
    InputTextModule,
    DialogModule,
    CheckboxModule,
    ContextMenuModule,
    ButtonModule,
  ],
  standalone: true,
})
export class BirthdayComponent implements OnInit, AfterViewInit {
  protected birthdayService = inject(BirthdayService);
  protected activatedRoute = inject(ActivatedRoute);
  protected router = inject(Router);
  protected modalService = inject(NgbModal);
  protected messageService = inject(MessageService);
  sanitizer = inject(DomSanitizer);
  private confirmationService = inject(ConfirmationService);
  protected viewService = inject(ViewService);

  @ViewChild('contextMenu') contextMenu!: ContextMenu;
  @ViewChild('superTable') superTable!: SuperTable;
  @ViewChild('menu') menu!: Menu;
  @ViewChild('expandedRow', { static: true }) expandedRowTemplate:
    | TemplateRef<any>
    | undefined;
  // The SuperTable in group mode manages its own detail tables

  currentQuery = '';
  rulesetJson = '';
  dataLoader: DataLoader<IBirthday>;
  groups: GroupDescriptor[] = [];
  viewName: string | null = null;
  views: { label: string; value: string }[] = [];
  globalFilterFields: string[] = ['lname', 'fname', 'dob', 'sign'];
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
        { label: 'Pisces', value: 'Pisces' },
      ],
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

  // New properties for super-table
  menuItems: MenuItem[] = [
    {
      label: 'Categorize',
      icon: 'pi pi-tags',
      command: () => this.openCategorizeDialog(),
    },
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

  // Selection and context menu state
  selectionMode: 'single' | 'multiple' | null | undefined = 'multiple';
  selection: IBirthday[] = [];

  // Categorize dialog state
  showCategorizeDialog = false;
  allCategories: string[] = [];
  filteredCategories: string[] = [];
  categoryFilterText = '';
  // category selection map: key -> 'checked' (all), 'indeterminate' (some), 'unchecked' (none)
  categoryState: Record<string, 'checked' | 'indeterminate' | 'unchecked'> = {};
  newCategoryText = '';
  newCategoryChecked = false;

  private lastSortEvent: any = null;
  private lastTableState: any;

  bDisplaySearchDialog = false;
  bDisplayBirthday = false;
  birthdayDialogTitle = '';
  birthdayDialogId: any = '';
  viewMode: 'grid' | 'group' = 'grid';
  showRowNumbers = false;
  private loadingSubscription?: Subscription;

  private syncSortFilterFromHeader(): void {
    if (this.superTable) {
      this.superTable.captureHeaderState();
      const sortEvent: any = (this.superTable as any).sortEvent;
      if (sortEvent) {
        this.predicate = sortEvent.field || sortEvent.sortField || this.predicate;
        this.ascending = (sortEvent.order ?? sortEvent.sortOrder) === 1;
      }
      this.lastSortEvent = sortEvent;
    }
  }

  constructor() {
    const fetchFunction: FetchFunction<IBirthday> = (queryParams: any) => {
      if (queryParams.bqlQuery) {
        const bql = queryParams.bqlQuery;
        delete queryParams.bqlQuery;
        return this.birthdayService.searchWithBql(bql, queryParams);
      }
      return this.birthdayService.query(queryParams);
    };
    this.dataLoader = new DataLoader<IBirthday>(fetchFunction);
  }

  loadRootGroups(restoreState: boolean = false): void {
    this.syncSortFilterFromHeader();
    if (!this.viewName) {
      this.groups = [];
      this.loadPage();
      this.viewMode = 'grid';
      if (restoreState) {
        setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
      }
      return;
    }

    const viewParams: any = { from: 0, pageSize: 1000, view: this.viewName! };
    const hasQuery = this.currentQuery && this.currentQuery.trim().length > 0;
    if (hasQuery) {
      this.birthdayService
        .searchWithBql(this.currentQuery.trim(), viewParams)
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            this.groups = hits.map((h) => ({
              name: h.categoryName,
              count: h.count,
              categories: null,
            }));
            this.viewMode = 'group';
            if (restoreState) {
              setTimeout(() => this.restoreState(), 500);
            } else {
              setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
            }
          } else {
            this.groups = [];
            const filter: any = { view: this.viewName! };
            filter.bqlQuery = this.currentQuery.trim();
            this.dataLoader.load(
              this.itemsPerPage,
              this.predicate,
              this.ascending,
              filter,
            );
            this.viewMode = 'grid';
            setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
          }
        });
    } else {
      this.birthdayService
        .searchView({ ...viewParams, query: '*' })
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            this.groups = hits.map((h) => ({
              name: h.categoryName,
              count: h.count,
              categories: null,
            }));
            this.viewMode = 'group';
            if (restoreState) {
              setTimeout(() => this.restoreState(), 500);
            } else {
              setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
            }
          } else {
            this.groups = [];
            const filter: any = { view: this.viewName!, query: '*' };
            this.dataLoader.load(
              this.itemsPerPage,
              this.predicate,
              this.ascending,
              filter,
            );
            this.viewMode = 'grid';
            setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
          }
        });
    }
  }

  loadViews(): void {
    this.viewService.query().subscribe((res) => {
      const body = res.body ?? [];
      this.views = body.map((v) => ({ label: v.name, value: v.id! }));
    });
  }

  @ViewChild(QueryInputComponent) queryInput!: QueryInputComponent;

  ngOnInit(): void {
    console.log('BirthdayComponent ngOnInit called');
    console.log('dataLoader:', this.dataLoader);
    console.log('dataLoader.loading$ initial:', this.dataLoader.loading$);
    
    this.loadViews();
    this.handleNavigation();
    
    // Subscribe to loading state changes for debugging
    this.dataLoader.loading$.subscribe(loading => {
      console.log('Loading state changed to:', loading);
    });
    
    // Auto-reset stuck loading state after 5 seconds
    setTimeout(() => {
      if (this.dataLoader['loadingSubject']?.getValue() === true) {
        console.warn('Loading state was stuck at true, auto-resetting...');
        this.forceResetLoading();
      }
    }, 5000);

    // Preload categories for dialog filtering; backend enforces keyword search
    this.birthdayService.getUniqueValues('categories').subscribe(res => {
      const values = res.body || [];
      // Deduplicate case-insensitively
      const uniq: Record<string, string> = {};
      values.forEach(v => {
        const k = (v || '').trim().toLowerCase();
        if (k && !uniq[k]) uniq[k] = v;
      });
      this.allCategories = Object.values(uniq).sort((a,b)=>a.localeCompare(b));
      this.filteredCategories = [...this.allCategories];
    });
  }

  ngAfterViewInit(): void {
    console.log('BirthdayComponent ngAfterViewInit called');
    this.onQueryChange(this.currentQuery);
  }

  onQueryChange(query: string, restoreState = false) {
    this.currentQuery = query;
    try {
      const rs = bqlToRuleset(query, this.queryInput.queryBuilderConfig);
      this.rulesetJson = JSON.stringify(rs, null, 2);
    } catch {
      this.rulesetJson = '';
    }
    if (this.viewName) {
      this.loadRootGroups(restoreState);
    } else {
      this.loadPage();
    }
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

  onSort(event: any): void {
    this.lastSortEvent = event;
  }

  refreshData(): void {
    console.log('refreshData called');

    try {
      this.syncSortFilterFromHeader();
      this.lastTableState = this.superTable.captureState();

      // Always re-issue the current query (BQL or lucene '*')
      this.onQueryChange(this.currentQuery, true);
    } catch (error) {
      console.error('Error in refreshData:', error);
      this.forceResetLoading();
    }
  }

  // Add a method to force reset the loading state
  forceResetLoading(): void {
    console.log('Force resetting loading state');
    
    // Reset loading state
    if (this.dataLoader['loadingSubject']) {
      this.dataLoader['loadingSubject'].next(false);
    }
    if (this.dataLoader['loadingMessageSubject']) {
      this.dataLoader['loadingMessageSubject'].next('');
    }
    
    // Reset internal DataLoader pagination state to prevent stuck states
    if (this.dataLoader['pitId']) {
      this.dataLoader['pitId'] = null;
    }
    if (this.dataLoader['searchAfter']) {
      this.dataLoader['searchAfter'] = [];
    }
    
    console.log('Loading state reset complete');
  }



  onViewChange(view: string | null): void {
    this.viewName = view;
    if (this.viewName) {
      this.loadRootGroups();
    } else {
      this.groups = [];
      this.viewMode = 'grid';
      this.loadPage();
      setTimeout(() => {
        this.superTable.applyCapturedHeaderState();
        (this.superTable as any).applyStoredStateToDetails();
      },1000);
    }
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

  // Update chips when selection changes across nested tables
  onCheckboxChange(): void {
    this.checkboxSelectedRows = this.selection || [];
    this.chipSelectedRows = this.checkboxSelectedRows.slice(0, 2);
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

  private restoreState(): void {
    this.superTable.restoreState(this.lastTableState);
    this.superTable.applyCapturedHeaderState();
  }

  showMenu(event: MouseEvent): void {
    this.menu.show(event);
  }

  onMenuShow(): void {
    // Using PrimeNG context menu; no custom hover/leave logic needed.
  }

  onChipClick(event: any): void {}

  // Right-click handling: only on grid/detail rows; accept both event or raw data
  onContextMenuSelect(dataOrEvent: any): void {
    const row: IBirthday | undefined = dataOrEvent && dataOrEvent.data ? dataOrEvent.data : dataOrEvent;
    if (!row) return;
    this.contextSelectedRow = row;
    // If no selection, act on the row under the cursor; else act on selection
    if (!this.selection || this.selection.length === 0) {
      this.selection = [row];
    }
  }

  onContextMenuMouseLeave(): void {
    this.contextMenu.hide();
  }

  onRemoveChip(event: any): void {
    this.chipSelectedRows = this.chipSelectedRows.filter(
      (row) => row.id !== event.id,
    );
  }

  delete(birthday: IBirthday): void {
    const modalRef = this.modalService.open(BirthdayDeleteDialogComponent, {
      size: 'lg',
      backdrop: 'static',
    });
    modalRef.componentInstance.birthday = birthday;
    // unsubscribe not needed because closed completes on modal close
    modalRef.closed.subscribe((reason) => {
      if (reason === ITEM_DELETED_EVENT) {
        this.loadPage();
      }
    });
  }

  loadPage(): void {
    this.syncSortFilterFromHeader();
    const filter: any = {};
    if (this.currentQuery && this.currentQuery.trim().length > 0) {
      filter.bqlQuery = this.currentQuery.trim();
    } else {
      filter.luceneQuery = '*';
    }
    if (this.viewName) {
      filter.view = this.viewName;
    }
    this.dataLoader.load(
      this.itemsPerPage,
      this.predicate,
      this.ascending,
      filter,
    );
    setTimeout(() => this.superTable.applyCapturedHeaderState(), 500);
  }

  protected sort(): string[] {
    const result = [this.predicate + ',' + (this.ascending ? ASC : DESC)];
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
      const pageSize = params.get('size');
      this.page = page !== null ? +page : 1;
      this.itemsPerPage = pageSize !== null ? +pageSize : this.itemsPerPage;
      const sort = (params.get(SORT) ?? data[DEFAULT_SORT_DATA]).split(',');
      this.predicate = sort[0];
      this.ascending = sort[1] === ASC;
      this.ngbPaginationPage = this.page;
      const filter: any = {};
      if (this.currentQuery && this.currentQuery.trim().length > 0) {
        filter.bqlQuery = this.currentQuery.trim();
      } else {
        filter.luceneQuery = '*';
      }
      if (this.viewName) {
        filter.view = this.viewName;
      }
      this.dataLoader.load(
        this.itemsPerPage,
        this.predicate,
        this.ascending,
        filter,
      );
    });
  }

  logSort(event: any): void {
    console.log('sort event', event);
  }

  groupQuery(group: GroupDescriptor): GroupData {
    this.syncSortFilterFromHeader();
    const path = group.categories
      ? [...group.categories, group.name]
      : [group.name];
    const params: any = {
      from: 0,
      pageSize: 1000,
      view: this.viewName!,
    };
    if (path.length >= 1) params.category = path[0];
    if (path.length >= 2) params.secondaryCategory = path[1];

    const groupData: GroupData = { mode: 'group', groups: [] };
    const hasQuery = this.currentQuery && this.currentQuery.trim().length > 0;
    if (hasQuery) {
      this.birthdayService
        .searchWithBql(this.currentQuery.trim(), params)
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            groupData.groups = hits.map((h) => ({
              name: h.categoryName,
              count: h.count,
              categories: path,
            }));
            groupData.mode = 'group';
          } else {
            const fetch: FetchFunction<IBirthday> = (queryParams: any) => {
              if (queryParams.bqlQuery) {
                const bql = queryParams.bqlQuery;
                delete queryParams.bqlQuery;
                return this.birthdayService.searchWithBql(bql, queryParams);
              }
              return this.birthdayService.query(queryParams);
            };
            const loader = new DataLoader<IBirthday>(fetch);
            const filter: any = { view: this.viewName! };
            filter.bqlQuery = this.currentQuery.trim();
            if (path.length >= 1) filter.category = path[0];
            if (path.length >= 2) filter.secondaryCategory = path[1];
            loader.load(
              this.itemsPerPage,
              this.predicate,
              this.ascending,
              filter,
            );
            groupData.mode = 'grid';
            groupData.loader = loader;
          }
        });
    } else {
      this.birthdayService
        .searchView({ ...params, query: '*' })
        .pipe(map((res) => res.body?.hits ?? []))
        .subscribe((hits: any[]) => {
          if (hits.length > 0 && (hits[0] as any).categoryName !== undefined) {
            groupData.groups = hits.map((h) => ({
              name: h.categoryName,
              count: h.count,
              categories: path,
            }));
            groupData.mode = 'group';
          } else {
            const fetch: FetchFunction<IBirthday> = (queryParams: any) => {
              if (queryParams.bqlQuery) {
                const bql = queryParams.bqlQuery;
                delete queryParams.bqlQuery;
                return this.birthdayService.searchWithBql(bql, queryParams);
              }
              return this.birthdayService.query(queryParams);
            };
            const loader = new DataLoader<IBirthday>(fetch);
            const filter: any = { view: this.viewName!, query: '*' };
            if (path.length >= 1) filter.category = path[0];
            if (path.length >= 2) filter.secondaryCategory = path[1];
            loader.load(
              this.itemsPerPage,
              this.predicate,
              this.ascending,
              filter,
            );
            groupData.mode = 'grid';
            groupData.loader = loader;
          }
        });
    }
    return groupData;
  }

  // Open categorize dialog from context menu
  openCategorizeDialog(): void {
    const rows = this.selection && this.selection.length > 0 ? this.selection : (this.contextSelectedRow ? [this.contextSelectedRow] : []);
    if (rows.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'No rows selected', detail: 'Select rows or right-click a row to categorize.' });
      return;
    }
    // Initialize tri-state map per category
    const lower = (s: string) => (s||'').toLowerCase();
    const rowsCats = rows.map(r => (r.categories || []).map(lower));
    const allLowerCats = new Set<string>(rowsCats.flat());
    const catInAll: Record<string, boolean> = {};
    this.allCategories.forEach(cat => {
      const lc = lower(cat);
      catInAll[lc] = rowsCats.every(cats => cats.includes(lc));
    });
    this.categoryState = {};
    this.allCategories.forEach(cat => {
      const lc = lower(cat);
      if (catInAll[lc]) this.categoryState[cat] = 'checked';
      else if (allLowerCats.has(lc)) this.categoryState[cat] = 'indeterminate';
      else this.categoryState[cat] = 'unchecked';
    });
    // Handle new category row
    this.newCategoryText = '';
    this.newCategoryChecked = false;
    this.categoryFilterText = '';
    this.filteredCategories = [...this.allCategories];
    this.showCategorizeDialog = true;
  }

  filterCategoriesList(): void {
    const q = (this.newCategoryText || '').trim().toLowerCase();
    if (!q) {
      this.filteredCategories = [...this.allCategories];
      return;
    }
    this.filteredCategories = this.allCategories.filter(c => c.toLowerCase().includes(q));
  }

  toggleCategory(cat: string): void {
    const state = this.categoryState[cat] || 'unchecked';
    // Both checked and indeterminate -> clicking removes category from any selected rows that have it
    if (state === 'checked' || state === 'indeterminate') {
      this.categoryState[cat] = 'unchecked';
    } else {
      // unchecked -> checked (assign to all rows)
      this.categoryState[cat] = 'checked';
    }
  }

  applyCategorize(): void {
    const rows = this.selection && this.selection.length > 0 ? this.selection : (this.contextSelectedRow ? [this.contextSelectedRow] : []);
    if (rows.length === 0) {
      this.showCategorizeDialog = false;
      return;
    }
    const lower = (s: string) => (s||'').toLowerCase();

    // Build adds/removes for existing categories
    const adds: string[] = [];
    const removes: string[] = [];
    Object.keys(this.categoryState).forEach(cat => {
      const st = this.categoryState[cat];
      if (st === 'checked') adds.push(cat);
      if (st === 'unchecked') {
        // unchecked by itself may mean no-op; we only remove if some rows had it originally
        // Determine if any selected row had this category
        const anyHad = rows.some(r => (r.categories || []).some(c => lower(c) === lower(cat)));
        if (anyHad) removes.push(cat);
      }
    });

    // Handle new category
    let newCat = (this.newCategoryText || '').trim();
    if (newCat) {
      // enforce case-insensitive uniqueness by matching against existing list
      const existing = this.allCategories.find(c => c.toLowerCase() === newCat.toLowerCase());
      if (existing) newCat = existing; // normalize casing
      if (this.newCategoryChecked) {
        if (!adds.some(a => a.toLowerCase() === newCat.toLowerCase())) {
          adds.push(newCat);
        }
      } else {
        // If user typed but did not check, treat as filter only; no-op
      }
    }

    const rowIds = rows.map(r => r.id).filter(Boolean) as string[];
    const summary = {
      rows: rowIds,
      add: adds,
      remove: removes,
    };
    // Log to console instead of showing a message
    // eslint-disable-next-line no-console
    console.log('Categorize preview', summary);
    this.showCategorizeDialog = false;
  }

  cancelCategorize(): void {
    this.showCategorizeDialog = false;
  }
}
