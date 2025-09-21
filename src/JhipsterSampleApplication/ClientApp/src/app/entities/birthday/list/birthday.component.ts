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
import { MenuModule } from 'primeng/menu';
import { ChipModule } from 'primeng/chip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

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
// Local minimal types to avoid cross-project import issues
type LocalRuleSet = { condition: string; rules: Array<LocalRuleSet | LocalRule>; name?: string; not?: boolean; isChild?: boolean };
type LocalRule = { field: string; operator: string; value?: any };
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { QueryLanguageSpec } from 'ngx-query-builder';

@Component({
  selector: 'jhi-birthday',
  templateUrl: './birthday.component.html',
  styleUrls: ['./birthday.component.scss'],
  schemas: [NO_ERRORS_SCHEMA],
  providers: [MessageService, ConfirmationService],
  imports: [
    CommonModule,
    FormsModule,
    HttpClientModule,
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
    MenuModule,
    ChipModule,
    ConfirmDialogModule,
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
  private http = inject(HttpClient);

  @ViewChild('contextMenu') contextMenu!: ContextMenu;
  @ViewChild('superTable') superTable!: SuperTable;
  @ViewChild('menu') menu!: Menu;
  @ViewChild('chipMenu') chipMenu!: Menu;
  @ViewChild('expandedRow', { static: true }) expandedRowTemplate:
    | TemplateRef<any>
    | undefined;
  // The SuperTable in group mode manages its own detail tables

  currentQuery = '';
  rulesetJson = '';
  spec: QueryLanguageSpec | undefined;
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
  iframeSafeSrcById: Record<string, any> = {};

  onRowExpand(event: { originalEvent: Event; data: any }): void {
    const row = event.data as IBirthday;
    const key = row?.id || JSON.stringify(row);
    this.expandedRowKeys[key] = true;
    // Delay iframe source assignment to avoid flicker during DOM expansion
    setTimeout(() => {
      const url = '/api/entity/birthday/html/' + row.id;
      this.iframeSafeSrcById[row.id!] = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    }, 50);
  }

  onRowCollapse(event: any): void {
    const row = event.data as IBirthday;
    const key = row?.id || JSON.stringify(row);
    delete this.expandedRowKeys[key];
    if (row?.id) {
      delete this.iframeSafeSrcById[row.id];
    }
  }

  // Highlighting support for expanded Wikipedia iframe only
  private getHighlightTermsFromBql(bql: string): string[] {
    const terms: string[] = [];
    if (!bql || !bql.trim()) return terms;
    try {
      const rs = bqlToRuleset(bql, this.queryInput.queryBuilderConfig) as LocalRuleSet;
      const visit = (node: LocalRuleSet | LocalRule) => {
        const asRule = node as LocalRule;
        if ((asRule as any).field !== undefined) {
          const field = (asRule.field || '').toLowerCase();
          const operator = (asRule.operator || '').toLowerCase();
          const value: any = (asRule as any).value;
          const pushVal = (v: any) => {
            if (v === null || v === undefined) return;
            const s = String(v).trim();
            if (s) terms.push(s);
          };
          // document contains "x" or generic value searches should highlight value
          if (operator && (operator.includes('contains') || operator.includes('like') || operator === '=' || operator === '==' || operator === 'in' || operator === '!in')) {
            if (Array.isArray(value)) value.forEach(pushVal);
            else pushVal(value);
          } else if (field === 'document') {
            if (Array.isArray(value)) value.forEach(pushVal);
            else pushVal(value);
          }
        } else {
          const asSet = node as LocalRuleSet;
          if (asSet && Array.isArray(asSet.rules)) asSet.rules.forEach(visit);
        }
      };
      visit(rs);
    } catch {}
    // Deduplicate and limit to reasonable length
    const dedup = Array.from(new Set(terms.map(t => t))).filter(t => t.length <= 256).slice(0, 50);
    return dedup;
  }

  onExpandedIframeLoad(id: string, ev: Event): void {
    try {
      const iframe = ev.target as HTMLIFrameElement;
      if (!iframe || !iframe.contentDocument) return;
      const doc = iframe.contentDocument;
      const terms = this.getHighlightTermsFromBql(this.currentQuery);
      if (!terms.length) return;
      // Inject simple CSS for highlight
      const style = doc.createElement('style');
      style.textContent = '.__bql-hl{background:yellow; color:#111;}';
      doc.head?.appendChild(style);
      // Build matchers from terms; support regex literals (/.../flags) with case sensitivity per 'i' flag
      const esc = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      const regexes: { re: RegExp }[] = [];
      const words: string[] = [];
      for (const t of terms) {
        const m = t.match(/^\/(.*)\/([a-z]*)$/);
        if (m) {
          const body = m[1];
          const flags = (m[2] || '').includes('i') ? 'gi' : 'g';
          try { regexes.push({ re: new RegExp(body, flags) }); } catch {}
        } else {
          words.push(esc(t));
        }
      }
      if (words.length) {
        try { regexes.push({ re: new RegExp('(' + words.join('|') + ')', 'gi') }); } catch {}
      }
      if (regexes.length === 0) return;
      // Tree-walk and collect text nodes to process
      const walker = doc.createTreeWalker(doc.body, NodeFilter.SHOW_TEXT, null as any);
      const textNodes: Text[] = [];
      let n: any;
      while ((n = walker.nextNode())) {
        if (n && n.nodeValue && String(n.nodeValue).trim().length > 0) {
          textNodes.push(n as Text);
        }
      }
      textNodes.forEach(tn => {
        const parent = tn.parentNode as HTMLElement | null;
        if (!parent) return;
        const text = tn.nodeValue || '';
        // Collect match ranges across all regexes
        type Range = { s: number; e: number };
        const ranges: Range[] = [];
        for (const { re } of regexes) {
          re.lastIndex = 0;
          let m: RegExpExecArray | null;
          while ((m = re.exec(text)) !== null) {
            const s = m.index;
            const e = s + (m[0]?.length || 0);
            if (e > s) ranges.push({ s, e });
            if (m[0].length === 0) re.lastIndex++;
          }
        }
        if (ranges.length === 0) return;
        // Merge overlapping
        ranges.sort((a, b) => a.s - b.s || a.e - b.e);
        const merged: Range[] = [];
        for (const r of ranges) {
          if (!merged.length || r.s > merged[merged.length - 1].e) {
            merged.push({ ...r });
          } else {
            merged[merged.length - 1].e = Math.max(merged[merged.length - 1].e, r.e);
          }
        }
        const frag = doc.createDocumentFragment();
        let last = 0;
        for (const r of merged) {
          if (r.s > last) frag.appendChild(doc.createTextNode(text.slice(last, r.s)));
          const mark = doc.createElement('mark');
          mark.className = '__bql-hl';
          mark.textContent = text.slice(r.s, r.e);
          frag.appendChild(mark);
          last = r.e;
        }
        if (last < text.length) frag.appendChild(doc.createTextNode(text.slice(last)));
        parent.replaceChild(frag, tn);
      });
    } catch {}
  }

  // New properties for super-table
  menuItems: MenuItem[] = [];
  contextSelectedRow: IBirthday | null = null;
  checkboxSelectedRows: IBirthday[] = [];
  chipSelectedRows: IBirthday[] = [];
  chipMenuRow: IBirthday | null = null;
  chipMenuIsCount: boolean = false;

  chipMenuModel: MenuItem[] = [];

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
  rowsToCategorizeCount = 0;

  private lastSortEvent: any = null;
  private lastTableState: any;

  bDisplaySearchDialog = false;
  bDisplayBirthday = false;
  birthdayDialogTitle = '';
  birthdayDialogId: any = '';
  dialogSafeSrc: any = null;
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
    this.viewService.queryByEntity('birthday').subscribe((res) => {
      const body = res.body ?? [];
      this.views = body.map((v) => ({ label: v.name, value: v.id! }));
    });
  }

  @ViewChild(QueryInputComponent) queryInput!: QueryInputComponent;

  ngOnInit(): void {
    // Load query builder spec from server
    this.birthdayService.getQueryBuilderSpec().subscribe({
      next: (spec: any) => (this.spec = spec),
      error: () => (this.spec = undefined),
    });
    // Restore views and initial navigation-driven state
    this.loadViews();
    this.handleNavigation();
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

    // Ensure group-mode list isn't filtered to a subset of groups
    try {
      if (this.superTable) {
        this.superTable.filterGlobal('');
      }
    } catch {}

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
      // Clear any previous group filter text when entering a grouped view
      try { this.superTable?.filterGlobal(''); } catch {}
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

  onChipMouseEnter(event: MouseEvent, row: IBirthday): void {
    this.chipMenuIsCount = false;
    this.chipMenuRow = row;
    this.setMenu(row);
    if (this.chipMenu) {
      this.chipMenu.show(event);
    }
  }

  onCountChipMouseEnter(event: MouseEvent): void {
    this.chipMenuIsCount = true;
    this.chipMenuRow = null;
    // Ensure menu text is generic for multi-categorize
    this.setMenu(null);
    if (this.chipMenu) {
      this.chipMenu.show(event);
    }
  }

  onChipMouseLeave(): void {
    if (this.chipMenu) {
      this.chipMenu.hide();
    }
  }

  trackById(index: number, row: IBirthday): any {
    return row?.id ?? index;
  }

  setMenu(birthday: IBirthday | null): void {
    const twoSelected = (this.selection?.length || 0) === 2;
    this.menuItems = this.buildContextMenuModel(birthday, twoSelected);
    // For chip menu build: pass hovered birthday if any
    this.updateChipMenuModel(twoSelected, birthday);
    this.contextSelectedRow = birthday;
  }

  private getDisplayName(row: IBirthday | null | undefined): string {
    if (!row) return '';
    const firstName = row.fname || '';
    const lastName = row.lname || '';
    return `${firstName} ${lastName}`.trim();
  }

  private buildContextMenuModel(birthday: IBirthday | null, twoSelected: boolean): MenuItem[] {
    const items: MenuItem[] = [];
    items.push({ label: 'Categorize', icon: 'pi pi-tags', command: () => this.openCategorizeDialog() });
    if (twoSelected && birthday) {
      const other = (this.selection as IBirthday[]).find(s => s.id !== birthday.id) || null;
      const relateTo = this.getDisplayName(other);
      if (relateTo) {
        items.push({ label: `Relate to ${relateTo}`, icon: 'pi pi-link', command: () => this.relateFromContext() });
      }
    }
    items.push({ label: 'View', icon: 'pi pi-search', command: () => this.viewIframeFromContext() });
    items.push({ label: 'Edit', icon: 'pi pi-pencil', command: () => this.editFromContext() });
    items.push({ label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteFromContext() });
    return items;
  }

  private updateChipMenuModel(twoSelected: boolean, hovered: IBirthday | null): void {
    // 3+ count chip: only Categorize/Delete
    if (this.chipMenuIsCount) {
      this.chipMenuModel = [
        { label: 'Categorize', icon: 'pi pi-tags', command: () => this.openCategorizeDialog() },
        { label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteFromChipMenu() },
      ];
      return;
    }
    // Row chip menu: include View/Edit and dynamic Relate when exactly two selected
    const model: MenuItem[] = [
      { label: 'Categorize', icon: 'pi pi-tags', command: () => this.openCategorizeDialog() },
    ];
    if (twoSelected && hovered) {
      const other = (this.selection as IBirthday[]).find(s => s.id !== hovered.id) || null;
      const relateTo = this.getDisplayName(other);
      if (relateTo) model.push({ label: `Relate to ${relateTo}`, icon: 'pi pi-link', command: () => this.relateFromContext() });
      // Add Both actions
      model.push({ label: 'Categorize Both', icon: 'pi pi-tags', command: () => this.openCategorizeBoth() });
    }
    model.push({ label: 'View', icon: 'pi pi-search', command: () => this.viewIframeFromContext() });
    model.push({ label: 'Edit', icon: 'pi pi-pencil', command: () => this.editFromContext() });
    model.push({ label: 'Delete', icon: 'pi pi-trash', command: () => this.deleteFromChipMenu() });
    if (twoSelected && hovered) {
      model.push({ label: 'Delete Both', icon: 'pi pi-trash', command: () => this.deleteBothFromChipMenu() });
    }
    this.chipMenuModel = model;
  }

  // View/Edit/Delete handlers for row context
  viewFromContext(): void {
    const row = this.contextSelectedRow || (this.selection && this.selection[0]);
    const id = row?.id;
    if (!id) return;
    this.router.navigate(['/birthday', id, 'view']);
  }

  // New: open resizable dialog with iframe for the selected row
  viewIframeFromContext(): void {
    const row = (this.contextSelectedRow || (this.selection && this.selection[0])) as IBirthday | undefined;
    const id = row?.id;
    if (!id) return;
    const firstName = row?.fname || '';
    const lastName = row?.lname || '';
    const fullName = `${firstName} ${lastName}`.trim();
    this.birthdayDialogId = id;
    this.birthdayDialogTitle = fullName || 'Details';
    this.bDisplayBirthday = true;
  }

  onBirthdayDialogShow(): void {
    // Delay iframe src to avoid flicker on dialog show
    const id = this.birthdayDialogId;
    this.dialogSafeSrc = null;
    setTimeout(() => {
      const url = '/api/entity/birthday/html/' + id;
      this.dialogSafeSrc = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    }, 50);
  }

  onBirthdayDialogHide(): void {
    this.dialogSafeSrc = null;
  }

  editFromContext(): void {
    const row = this.contextSelectedRow || (this.selection && this.selection[0]);
    const id = row?.id;
    if (!id) return;
    this.router.navigate(['/birthday', id, 'edit']);
  }

  deleteFromContext(): void {
    const row = this.contextSelectedRow || (this.selection && this.selection[0]);
    const id = row?.id;
    if (!id) return;
    this.birthdayService.delete(id).subscribe({ next: () => this.refreshData(), error: () => this.refreshData() });
  }

  relateFromContext(): void {
    // Placeholder: implement relate flow later
  }

  openCategorizeBoth(): void {
    // Force count mode to target all selected rows
    this.chipMenuIsCount = true;
    this.openCategorizeDialog();
  }

  deleteBothFromChipMenu(): void {
    const selected = (this.selection || []) as IBirthday[];
    if (!selected.length) return;
    this.chipMenuIsCount = true;
    this.deleteFromChipMenu();
  }

  private restoreState(): void {
    this.superTable.restoreState(this.lastTableState);
    this.superTable.applyCapturedHeaderState();
  }

  showMenu(event: MouseEvent): void {
    this.menu.show(event);
  }

  onMenuShow(): void {
    // Ensure menu items are up-to-date for the current context row
    this.setMenu(this.contextSelectedRow);
  }

  onChipClick(event: any): void {}

  // Right-click handling: only on grid/detail rows; accept both event or raw data
  onContextMenuSelect(dataOrEvent: any): void {
    const row: IBirthday | undefined = dataOrEvent && dataOrEvent.data ? dataOrEvent.data : dataOrEvent;
    if (!row) return;
    this.contextSelectedRow = row;
    // Do NOT modify checkbox selection from a right-click. Just build the menu for the context row.
    this.setMenu(row);
  }

  onContextMenuMouseLeave(): void {
    this.contextMenu.hide();
  }

  onRemoveChipLegacy(event: any): void {
    this.chipSelectedRows = this.chipSelectedRows.filter(
      (row) => row.id !== event.id,
    );
  }

  onRemoveChip(row: IBirthday): void {
    const id = row?.id;
    if (!id) return;
    // Remove from selection array and notify table
    this.selection = (this.selection || []).filter(r => (r?.id ?? r) !== id);
    this.onCheckboxChange();
  }

  // Remove the count chip: clear all selections
  onRemoveCountChip(): void {
    this.selection = [];
    this.onCheckboxChange();
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
    const rows = this.chipMenuIsCount
      ? (this.selection || [])
      : this.chipMenuRow
      ? [this.chipMenuRow]
      : this.selection && this.selection.length > 0
      ? this.selection
      : this.contextSelectedRow
      ? [this.contextSelectedRow]
      : [];
    // reset chip override flags after use
    this.chipMenuRow = null;
    this.chipMenuIsCount = false;
    if (rows.length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'No rows selected', detail: 'Select rows or right-click a row to categorize.' });
      return;
    }
    this.rowsToCategorizeCount = rows.length;
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
        // only remove if some rows had it originally
        const anyHad = rows.some(r => (r.categories || []).some(c => lower(c) === lower(cat)));
        if (anyHad) removes.push(cat);
      }
    });

    // Handle new category
    let newCat = (this.newCategoryText || '').trim();
    if (newCat) {
      const existing = this.allCategories.find(c => c.toLowerCase() === newCat.toLowerCase());
      if (existing) newCat = existing; // normalize casing
      if (this.newCategoryChecked) {
        if (!adds.some(a => a.toLowerCase() === newCat.toLowerCase())) {
          adds.push(newCat);
        }
      }
    }

    const rowIds = rows.map(r => r.id).filter(Boolean) as string[];
    const payload = { rows: rowIds, add: adds, remove: removes };
    this.messageService.clear();
    this.messageService.add({ severity: 'info', summary: 'Categorizing...', detail: `Updating ${rowIds.length} item(s)` });
    this.birthdayService.categorizeMultiple(payload).subscribe({
      next: (res) => {
        const ok = res.body?.success;
        const msg = res.body?.message || 'Completed';
        this.messageService.add({ severity: ok ? 'success' : 'warn', summary: 'Categorize', detail: msg });
        this.showCategorizeDialog = false;
        // Refresh data preserving header state and row expansions/scroll
        this.refreshData();
      },
      error: (err) => {
        this.messageService.add({ severity: 'error', summary: 'Categorize failed', detail: (err?.message || 'Error') });
        this.showCategorizeDialog = false;
      }
    });
  }

  cancelCategorize(): void {
    this.showCategorizeDialog = false;
  }

  hasCategorizeChanges(): boolean {
    // New category will only count if text is present and checkbox is checked
    const hasNewCat = (this.newCategoryText || '').trim().length > 0 && this.newCategoryChecked;
    if (hasNewCat) return true;
    // Any existing category that is not indeterminate implies a definite change
    // 'checked' -> ensure present for all; 'unchecked' -> remove from rows that have it
    return Object.values(this.categoryState).some(st => st === 'checked' || st === 'unchecked');
  }

  // Delete handler invoked from chip hover menu
  deleteFromChipMenu(): void {
    // If count chip, delete all selected; else delete the single chip row
    const rowsToDelete: IBirthday[] = this.chipMenuIsCount
      ? (this.selection || [])
      : this.chipMenuRow
      ? [this.chipMenuRow]
      : [];
    this.chipMenuRow = null;
    this.chipMenuIsCount = false;
    if (!rowsToDelete || rowsToDelete.length === 0) return;

    const ids = rowsToDelete.map(r => r.id).filter(Boolean) as string[];
    if (ids.length === 0) return;

    const proceed = () => {
      this.messageService.add({ severity: 'info', summary: 'Delete', detail: `Deleting ${ids.length} item(s)...` });
      const deleteNext = (remaining: string[]) => {
        if (remaining.length === 0) {
          this.refreshData();
          return;
        }
        const id = remaining.shift()!;
        this.birthdayService.delete(id).subscribe({
          next: () => deleteNext(remaining),
          error: () => deleteNext(remaining),
        });
      };
      deleteNext([...ids]);
    };

    if (ids.length > 1) {
      this.confirmationService.confirm({
        header: 'Confirm Delete',
        icon: 'pi pi-exclamation-triangle',
        message: `You are about to delete ${ids.length} records, do you want to proceed?`,
        acceptLabel: 'Yes',
        rejectLabel: 'No',
        accept: () => proceed(),
      });
    } else {
      proceed();
    }
  }
}
