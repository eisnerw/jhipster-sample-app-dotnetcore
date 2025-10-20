import {
  Component,
  EventEmitter,
  Input,
  Output,
  OnInit,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
  ElementRef,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpParams, HttpClientModule } from '@angular/common/http';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TreeModule } from 'primeng/tree';
import { AutoCompleteModule, AutoComplete } from 'primeng/autocomplete';
import {
  QueryBuilderModule,
  QueryBuilderConfig,
  RuleSet,
  Rule,
  QueryLanguageSpec,
  QueryBuilderComponent,
} from 'ngx-query-builder';
import {
  bqlToRuleset,
  rulesetToBql,
  validateBql,
  validateRuleset,
} from '../bql';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom, Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { EditRulesetDialogComponent } from './edit-ruleset-dialog.component';
import { BqlAutocompleteService, AutocompleteSuggestion } from '../services/bql-autocomplete.service';

interface NamedQuery {
  id?: number;
  name: string;
  text: string;
  owner?: string;
  entity?: string;
}

@Component({
  selector: 'lib-query-input',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HttpClientModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TreeModule,
    AutoCompleteModule,
    QueryBuilderModule,
  ],
  providers: [BqlAutocompleteService],
  styleUrls: ['./query-input.component.scss'],
  templateUrl: './query-input.component.html',
})
export class QueryInputComponent implements OnInit, OnChanges, OnDestroy {
  private dialog = inject(MatDialog);
  private http = inject(HttpClient);
  private autocompleteService = inject(BqlAutocompleteService);

  @ViewChild('builder') builder?: QueryBuilderComponent;
  @ViewChild('editBox') editBox?: AutoComplete;

  @Input() placeholder = 'BQL';
  @Input() query = '';
  @Input() config?: QueryBuilderConfig;
  // Optional JSON-driven language spec
  @Input() spec?: QueryLanguageSpec | string;
  @Input() allowNot = true;
  @Input() allowConvertToRuleset = true;
  @Input() allowRuleUpDown = true;
  @Input() ruleName = 'Rule';
  @Input() rulesetName = 'Ruleset';
  @Input() defaultRuleAttribute: string | null = null;
  @Input() namedQueryEntity: string | null = null;
  @Input() historyEntity: string | null = null;
  @Output() queryChange = new EventEmitter<string>();

  editing = false;
  showBuilder = false;
  builderQuery: RuleSet = { condition: 'and', rules: [] };
  namedRulesets: Record<string, RuleSet> = {};
  private namedQueryIds: Record<string, number> = {};
  validQuery = true;
  private previousQuery = '';
  private history: string[] = [];
  private historyIndex = -1;

  // Autocomplete state
  autocompleteSuggestions: AutocompleteSuggestion[] = [];
  showAutocomplete = false;
  selectedSuggestionIndex = -1;
  private inputSubject = new Subject<string>();
  private destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.onQueryChange();
    this.loadNamedQueries();
    // Pre-load history so arrow navigation is responsive when editing starts
    this.loadHistory();
    
    // Set up autocomplete input debouncing
    this.inputSubject
      .pipe(
        debounceTime(150),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.updateAutocompleteSuggestions();
      });
  }

  ngOnChanges(changes: SimpleChanges): void {
    // When entity context changes, reset and reload history from backend
    if (changes['historyEntity'] && !changes['historyEntity'].firstChange) {
      this.history = [];
      this.historyIndex = -1;
      this.loadHistory();
    }
    
    // Clear autocomplete cache when config or spec changes
    if ((changes['config'] && !changes['config'].firstChange) || 
        (changes['spec'] && !changes['spec'].firstChange)) {
      this.autocompleteService.clearCache();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadNamedQueries(): void {
    let params = new HttpParams();
    if (this.namedQueryEntity) {
      params = params.set('entity', this.namedQueryEntity);
    }
    this.http
      .get<NamedQuery[]>('/api/NamedQueries', { params })
      .subscribe(queries => {
        queries.forEach(q => {
          try {
            this.namedRulesets[q.name] = bqlToRuleset(q.text, this.queryBuilderConfig);
            if (q.id !== undefined) {
              this.namedQueryIds[q.name] = q.id;
            }
          } catch {
            // Ignore malformed queries
          }
        });
      });
  }

  startEdit() {
    this.previousQuery = this.query;
    this.editing = true;
    this.onQueryChange();
    // Focus the input immediately after switching to edit mode
    setTimeout(() => {
      const el = this.getInputEl();
      if (el) {
        el.focus();
        try {
          const len = el.value?.length ?? 0;
          el.setSelectionRange(len, len);
        } catch {}
      }
      // If starting with an empty query, surface field suggestions immediately
      if (!this.query || this.query.trim() === '') {
        this.updateAutocompleteSuggestions(true);
      }
    });
  }

  clearQuery(event?: Event) {
    if (event) {
      event.stopPropagation();
    }
    this.query = '';
    this.onQueryChange();
    this.queryChange.emit(this.query);
  }

  onQueryChange() {
    this.validQuery = validateBql(this.query, this.queryBuilderConfig);
  }

  // Minimal fallback so the builder works before a spec/config is provided
  defaultConfig: QueryBuilderConfig = {
    fields: {
      document: {
        name: 'Document',
        type: 'string',
        operators: ['contains', '!contains', 'like', '!like'],
        defaultOperator: 'contains',
      },
    },
  };

  get queryBuilderConfig(): QueryBuilderConfig {
    // Start from provided config or minimal default
    const base = { ...(this.config || this.defaultConfig) } as QueryBuilderConfig;

    // If a spec is provided, merge fields/entities and hydrate operators from operatorMap
    if (this.spec) {
      let specObj: QueryLanguageSpec | null = null;
      if (typeof this.spec === 'string') {
        try {
          specObj = JSON.parse(this.spec) as QueryLanguageSpec;
        } catch {
          specObj = null;
        }
      } else {
        specObj = this.spec as QueryLanguageSpec;
      }
      if (specObj && specObj.fields) {
        const opMap = specObj.operatorMap || {};
        const hydratedFields: Record<string, any> = {};
        Object.keys(specObj.fields).forEach((k) => {
          const f = { ...(specObj!.fields as any)[k] };
          if ((!f.operators || f.operators.length === 0) && f.type && opMap[f.type]) {
            f.operators = [...opMap[f.type]];
          }
          hydratedFields[k] = f;
        });
        base.fields = hydratedFields as any;
        if (specObj.entities) {
          base.entities = specObj.entities;
        }
      }
    }

    return {
      ...base,
      listNamedRulesets: this.listNamedRulesets.bind(this),
      getNamedRuleset: this.getNamedRuleset.bind(this),
      saveNamedRuleset: this.saveNamedRuleset.bind(this),
      deleteNamedRuleset: this.deleteNamedRuleset.bind(this),
      editNamedRuleset: this.editNamedRuleset.bind(this),
      customCollapsedSummary: this.collapsedSummary.bind(this),
    } as QueryBuilderConfig;
  }

  clickSearch() {
    this.builderQuery = this.parseQuery(this.query);

    // Ensure the query has the required structure
    if (!this.builderQuery.condition) {
      this.builderQuery.condition = 'and';
    }

    this.showBuilder = true;
  }

  builderApplied(q: RuleSet) {
    this.query = this.stringifyQuery(q);
    this.onQueryChange();
    this.queryChange.emit(this.query);
    this.showBuilder = false;
  }

  cancelEdit() {
    this.editing = false;
    this.query = this.previousQuery;
    this.onQueryChange();
    this.closeAutocomplete();
  }

  acceptEdit() {
    if (!this.validQuery) {
      return;
    }
    this.editing = false;
    this.previousQuery = this.query;
    this.queryChange.emit(this.query);
    this.closeAutocomplete();
    if (this.historyEntity && this.query) {
      if (this.history[0] !== this.query) {
        this.http.post('/api/Histories', { entity: this.historyEntity, text: this.query }).subscribe();
        this.history.unshift(this.query);
      }
      this.historyIndex = -1;
    }
  }

  onEnter(event: any) {
    if (this.validQuery) {
      this.acceptEdit();
    } else {
      event.preventDefault();
    }
  }

  showPrevHistory(event: any) {
    event.preventDefault();
    if (!this.history.length) {
      return;
    }
    if (this.historyIndex < this.history.length - 1) {
      this.historyIndex++;
      this.query = this.history[this.historyIndex];
    }
    this.onQueryChange();
  }

  showNextHistory(event: any) {
    event.preventDefault();
    if (!this.history.length) {
      return;
    }
    if (this.historyIndex > 0) {
      this.historyIndex--;
      this.query = this.history[this.historyIndex];
    } else {
      this.historyIndex = -1;
      this.query = '';
    }
    this.onQueryChange();
  }

  private loadHistory(): void {
    if (!this.historyEntity) {
      return;
    }
    const url = `/api/entity/${encodeURIComponent(this.historyEntity)}/bql-history`;
    this.http
      .get<HistoryDto[]>(url)
      .subscribe(res => {
        this.history = (res || []).map(h => h.text).filter(t => !!t);
        this.historyIndex = -1;
      });
    }

  parseQuery(text: string): RuleSet {
    const trimmed = text.trim();
    if (!trimmed) {
      const fields = Object.keys(this.queryBuilderConfig.fields);
      const attr = this.defaultRuleAttribute || fields[0];
      let operator: string | undefined;
      if (attr) {
        const fieldCfg = this.queryBuilderConfig.fields[attr];
        if (fieldCfg) {
          if (fieldCfg.defaultOperator !== undefined) {
            operator =
              typeof fieldCfg.defaultOperator === 'function'
                ? fieldCfg.defaultOperator()
                : fieldCfg.defaultOperator;
          } else if (fieldCfg.operators && fieldCfg.operators.length) {
            operator = fieldCfg.operators[0];
          }
        }
      }
      const rule = attr ? ({ field: attr, operator } as Rule) : undefined;
      return { condition: 'and', rules: rule ? [rule] : [] };
    }

    try {
      return bqlToRuleset(trimmed, this.queryBuilderConfig);
    } catch {
      return { condition: 'and', rules: [] };
    }
  }

  stringifyQuery(obj: RuleSet): string {
    try {
      const cleanQuery = this.cleanQuery(obj);
      return rulesetToBql(cleanQuery, this.queryBuilderConfig);
    } catch {
      return '';
    }
  }

  private cleanQuery(query: RuleSet): RuleSet {
    if (!query || typeof query !== 'object') {
      return { condition: 'and', rules: [] };
    }

    const cleaned: RuleSet = {
      condition: query.condition || 'and',
      rules: (query.rules || [])
        .map((item: any) => {
          // Check if this is a nested ruleset
          if (item.condition && item.rules) {
            // Recursively clean nested rulesets
            return this.cleanQuery(item);
          }
          // This is a regular rule - validate it has required fields
          else if (item.field && item.operator) {
            const op = (item.operator || '').toLowerCase();
            const isUnary = op === 'is null' || op === 'is not null' || op === 'exists' || op === '!exists';
            if (isUnary || (item.value !== undefined && item.value !== '')) {
              return item;
            }
          }
          // Invalid rule, exclude it
          return null;
        })
        .filter((item) => item !== null),
    };

    // Include additional properties if they exist
    if (query.not !== undefined) {
      cleaned.not = query.not;
    }
    if (query.name !== undefined) {
      cleaned.name = query.name;
    }
    if (query.collapsed !== undefined) {
      cleaned.collapsed = query.collapsed;
    }
    if (query.isChild !== undefined) {
      cleaned.isChild = query.isChild;
    }

    return cleaned;
  }

  applyQuery() {
    this.builderApplied(this.builderQuery);
  }

  cancelQuery() {
    this.showBuilder = false;
  }

  listNamedRulesets(): string[] {
    return Object.keys(this.namedRulesets);
  }

  getNamedRuleset(name: string): RuleSet {
    return JSON.parse(JSON.stringify(this.namedRulesets[name]));
  }

  saveNamedRuleset(rs: RuleSet) {
    if (rs.name) {
      this.namedRulesets[rs.name] = JSON.parse(JSON.stringify(rs));
      const rsClone = JSON.parse(JSON.stringify(rs));
      delete rsClone.name;
      const bql = rulesetToBql(rsClone, this.queryBuilderConfig);
      const payload: NamedQuery = {
        name: rs.name,
        text: bql,
        entity: this.namedQueryEntity ?? undefined,
      };
      const id = this.namedQueryIds[rs.name];
      if (id) {
        this.http.put(`/api/NamedQueries/${id}`, { ...payload, id }).subscribe();
      } else {
        this.http.post<NamedQuery>('/api/NamedQueries', payload).subscribe(res => {
          if (res.id !== undefined) {
            this.namedQueryIds[rs.name!] = res.id;
          }
        });
      }
    }
  }

  deleteNamedRuleset(name: string) {
    delete this.namedRulesets[name];
    const id = this.namedQueryIds[name];
    if (id !== undefined) {
      this.http.delete(`/api/NamedQueries/${id}`).subscribe();
      delete this.namedQueryIds[name];
    }
  }

  async editNamedRuleset(
    rs: RuleSet,
    ancestors: string[] = [],
  ): Promise<RuleSet | null> {
    const result = await firstValueFrom(
      this.dialog
        .open(EditRulesetDialogComponent, {
          data: {
            ruleset: JSON.parse(JSON.stringify(rs)),
            rulesetName: this.rulesetName,
            validate: (r: any) =>
              !!r &&
              typeof r === 'object' &&
              Array.isArray(r.rules) &&
              r.rules.length > 0 &&
              validateRuleset(r, this.queryBuilderConfig, [
                ...ancestors,
                rs.name ?? '',
              ]),
            config: this.queryBuilderConfig,
          },
          width: '800px',
          panelClass: 'resizable-dialog',
          autoFocus: false,
        })
        .afterClosed(),
    );
    return result || null;
  }

  collapsedSummary(ruleset: RuleSet): string {
    const names = new Set<string>();
    const walk = (rs: RuleSet) => {
      rs.rules.forEach((r) => {
        if ((r as Rule).field) {
          const field = this.queryBuilderConfig.fields[(r as Rule).field];
          names.add(field?.name || (r as Rule).field);
        } else if ((r as RuleSet).rules) {
          walk(r as RuleSet);
        }
      });
    };
    walk(ruleset);
    return Array.from(names).join(', ');
  }

  /**
   * Called on input change to trigger autocomplete
   */
  private savedQueryForSelection = '';
  private savedCursorForSelection = 0;
  private savedReplaceFrom = 0;
  private savedReplaceTo = 0;

  onInputChange(event: any): void {
    // Save the current query and cursor position before any selection
    // This is needed because PrimeNG will modify the query before onSuggestionSelect is called
    this.savedQueryForSelection = this.query;
    this.savedCursorForSelection = this.getInputEl()?.selectionStart ?? this.query.length;
    
    this.inputSubject.next(this.query);
  }

  /**
   * Handles model changes from typing
   */
  onModelChange(value: string): void {
    this.onQueryChange();
    // Trigger autocomplete update without overwriting savedQueryForSelection
    this.inputSubject.next(this.query);
  }

  /**
   * Handles selection from autocomplete dropdown
   * PrimeNG has already modified this.query by the time this is called,
   * so we need to restore the saved state before calling selectSuggestion
   */
  onSuggestionSelect(event: any): void {
    const suggestion: AutocompleteSuggestion | null = event?.value ?? null;
    if (!suggestion) return;

    // Build new text from the last stable snapshot and replace range
    const base = this.savedQueryForSelection ?? this.query;
    const from = this.savedReplaceFrom ?? 0;
    const to = this.savedReplaceTo ?? (this.savedCursorForSelection ?? base.length);

    let valueToInsert = suggestion.value;

    // Spacing and quoting rules mirror selectSuggestion()
    if (suggestion.type === 'operator') {
      if (from > 0 && base[from - 1] !== ' ') {
        valueToInsert = ' ' + valueToInsert;
      }
      valueToInsert = valueToInsert + ' ';
    }

    if (suggestion.type === 'value') {
      const needsQuotes = /\s/.test(valueToInsert) || valueToInsert.includes(',');
      if (needsQuotes && !valueToInsert.startsWith('"')) {
        valueToInsert = '"' + valueToInsert + '"';
      }
      const beforeToken = base.substring(0, from);
      const inMultiValue = /\b(!?IN)\s*\([^)]*$/.test(beforeToken.toUpperCase());
      if (!inMultiValue) {
        valueToInsert = valueToInsert + ' ';
      }
    }

    if (suggestion.type === 'field') {
      valueToInsert = valueToInsert + ' ';
    }

    const newText = base.substring(0, from) + valueToInsert + base.substring(to);
    const newCursorPos = from + valueToInsert.length;

    this.query = newText;
    this.closeAutocomplete();
    this.onQueryChange();

    setTimeout(() => {
      const el = this.getInputEl();
      if (el) {
        el.focus();
        try { el.setSelectionRange(newCursorPos, newCursorPos); } catch {}
      }
      if (suggestion.type === 'field') {
        this.updateAutocompleteSuggestions(true);
        this.editBox?.show();
      }
    }, 0);
  }

  /**
   * Updates autocomplete suggestions based on current input
   */
  private updateAutocompleteSuggestions(forceShow = false): void {
    if (!this.editing) {
      this.showAutocomplete = false;
      return;
    }

    const cursorPosition = this.getInputEl()?.selectionStart ?? this.query.length;
    
    this.autocompleteSuggestions = this.autocompleteService.getSuggestions(
      this.query,
      cursorPosition,
      this.queryBuilderConfig
    );

    this.showAutocomplete = this.autocompleteSuggestions.length > 0;

    // Always capture a fresh snapshot whenever suggestions are visible so that
    // onSuggestionSelect restores the latest text/caret reliably.
    if (this.showAutocomplete) {
      this.savedQueryForSelection = this.query;
      this.savedCursorForSelection = cursorPosition;
      // Determine token replace range [from,to) for current caret
      let from = cursorPosition;
      while (from > 0) {
        const ch = this.query[from - 1];
        if (ch === ' ' || ch === '&' || ch === '|' || ch === '(' || ch === ')' || ch === ',') {
          break;
        }
        from--;
      }
      this.savedReplaceFrom = from;
      this.savedReplaceTo = cursorPosition;
    }

    // If we have suggestions and either caller requested to force show or
    // we're at the start/after a group open, ensure the panel is visible
    if (this.showAutocomplete && (forceShow || cursorPosition === 0 || /\(\s*$/.test(this.query.substring(0, cursorPosition)))) {
      // Defer to let suggestions bind before showing panel
      setTimeout(() => this.editBox?.show(), 0);
    }
    this.selectedSuggestionIndex = -1;
  }

  /** Returns the native input element inside AutoComplete */
  private getInputEl(): HTMLInputElement | null {
    return (this.editBox?.inputEL?.nativeElement as HTMLInputElement) || null;
  }

  /** Show suggestions when input gains focus (esp. for empty query) */
  onInputFocus(): void {
    this.updateAutocompleteSuggestions(true);
  }

  /**
   * Inserts selected suggestion into query
   */
  selectSuggestion(suggestion: AutocompleteSuggestion): void {
    const cursorPosition = this.getInputEl()?.selectionStart ?? this.query.length;
    
    // Find the start of the current token to replace
    let tokenStart = cursorPosition;
    while (tokenStart > 0) {
      const char = this.query[tokenStart - 1];
      // Stop at whitespace, logical operators, or comma
      if (char === ' ' || char === '&' || char === '|' || char === '(' || char === ')' || char === ',') {
        break;
      }
      tokenStart--;
    }

    // Build the value to insert
    let valueToInsert = suggestion.value;
    
    // Handle spacing for operators
    if (suggestion.type === 'operator') {
      // Add space before if not already present
      if (tokenStart > 0 && this.query[tokenStart - 1] !== ' ') {
        valueToInsert = ' ' + valueToInsert;
      }
      // Add space after
      valueToInsert = valueToInsert + ' ';
    }
    
    // Handle quoting for string values
    if (suggestion.type === 'value') {
      const needsQuotes = /\s/.test(valueToInsert) || valueToInsert.includes(',');
      if (needsQuotes && !valueToInsert.startsWith('"')) {
        valueToInsert = '"' + valueToInsert + '"';
      }
      
      // Check if we're inside an IN or !IN operator (look for opening paren before cursor)
      const beforeToken = this.query.substring(0, tokenStart);
      const inMultiValue = /\b(!?IN)\s*\([^)]*$/.test(beforeToken.toUpperCase());
      
      if (inMultiValue) {
        // Don't add anything after value - user will type comma or )
        // valueToInsert stays as is
      } else {
        // Add space after value for next condition
        valueToInsert = valueToInsert + ' ';
      }
    }

    // Handle field names - add space after
    if (suggestion.type === 'field') {
      valueToInsert = valueToInsert + ' ';
    }

    // Insert the value
    const before = this.query.substring(0, tokenStart);
    const after = this.query.substring(cursorPosition);
    this.query = before + valueToInsert + after;

    // Update cursor position
    const newCursorPosition = tokenStart + valueToInsert.length;
    
    // Close autocomplete
    this.closeAutocomplete();
    
    // Validate the query
    this.onQueryChange();

    // Set cursor position after Angular updates the view
    setTimeout(() => {
      const el = this.getInputEl();
      if (el) {
        el.focus();
        try {
          el.setSelectionRange(newCursorPosition, newCursorPosition);
        } catch {}
      }
      // If a field was just inserted, immediately surface operator suggestions
      if (suggestion.type === 'field') {
        this.updateAutocompleteSuggestions(true);
        this.editBox?.show();
      }
    });
  }

  /**
   * Closes autocomplete dropdown
   */
  closeAutocomplete(): void {
    this.showAutocomplete = false;
    this.autocompleteSuggestions = [];
    this.selectedSuggestionIndex = -1;
  }

  /**
   * Handles input blur to close autocomplete
   */
  onInputBlur(): void {
    // Delay closing to allow click events on suggestions to fire
    setTimeout(() => {
      this.closeAutocomplete();
    }, 200);
  }

  /**
   * Handles general keydown events
   */
  onKeydown(event: KeyboardEvent): void {
    // Gesture: Ctrl+Space forces suggestions (useful at start of query)
    if ((event.key === ' ' || event.code === 'Space') && event.ctrlKey) {
      event.preventDefault();
      this.updateAutocompleteSuggestions(true);
      return;
    }
    // After logical operators &, | we want fields and a fresh snapshot
    if (event.key === '&' || event.key === '|') {
      setTimeout(() => this.updateAutocompleteSuggestions(true), 0);
    }
    // If user types a space after a valid field name, open operator suggestions
    if (event.key === ' ' && !event.ctrlKey) {
      const cursorPosition = this.getInputEl()?.selectionStart ?? this.query.length;
      const beforeCursor = this.query.substring(0, cursorPosition);
      const trimmed = beforeCursor.trimEnd();
      const m = /(\w+)$/.exec(trimmed);
      if (m) {
        const field = m[1];
        if (this.queryBuilderConfig.fields && this.queryBuilderConfig.fields[field]) {
          setTimeout(() => {
            this.updateAutocompleteSuggestions(true);
            this.editBox?.show();
          }, 0);
        }
      }
    }
    // Handle opening parenthesis after IN/!IN to trigger autocomplete
    if (event.key === '(') {
      // Let the ( be typed, then trigger autocomplete
      setTimeout(() => {
        this.updateAutocompleteSuggestions(true);
        this.editBox?.show();
      }, 10);
    }
    
    // Handle comma inside IN/!IN to trigger autocomplete for next value
    if (event.key === ',') {
      const cursorPosition = this.getInputEl()?.selectionStart ?? this.query.length;
      const beforeCursor = this.query.substring(0, cursorPosition);
      
      // Check if we're inside an IN or !IN operator
      if (/\b(!?IN)\s*\([^)]*$/.test(beforeCursor.toUpperCase())) {
        // Let the comma be typed, then trigger autocomplete
        setTimeout(() => {
          this.updateAutocompleteSuggestions();
        }, 10);
      }
    }
    
    // Handle closing parenthesis for IN/!IN operators
    if (event.key === ')') {
      const cursorPosition = this.getInputEl()?.selectionStart ?? this.query.length;
      const beforeCursor = this.query.substring(0, cursorPosition);
      
      // Check if we're inside an IN or !IN operator and there's a trailing comma
      if (/\b(!?IN)\s*\([^)]*,\s*$/.test(beforeCursor.toUpperCase())) {
        // Remove trailing comma and space before inserting )
        const trimmed = beforeCursor.replace(/,\s*$/, '');
        this.query = trimmed + this.query.substring(cursorPosition);
        
        // Update cursor position
        setTimeout(() => {
          const el = this.getInputEl();
          if (el) {
            const newPos = trimmed.length;
            el.setSelectionRange(newPos, newPos);
          }
        }, 0);
      }
    }
  }

  /**
   * Handles keyboard navigation in autocomplete dropdown
   */
  onAutocompleteKeydown(event: KeyboardEvent): void {
    // Only handle keyboard navigation when autocomplete is open
    if (!this.showAutocomplete || this.autocompleteSuggestions.length === 0) {
      // When autocomplete is closed, allow normal history navigation
      if (event.key === 'ArrowUp') {
        this.showPrevHistory(event);
      } else if (event.key === 'ArrowDown') {
        this.showNextHistory(event);
      }
      return;
    }

    // Handle keyboard navigation when autocomplete is open
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.selectedSuggestionIndex = Math.min(
          this.selectedSuggestionIndex + 1,
          this.autocompleteSuggestions.length - 1
        );
        break;

      case 'ArrowUp':
        event.preventDefault();
        this.selectedSuggestionIndex = Math.max(
          this.selectedSuggestionIndex - 1,
          0
        );
        break;

      case 'Enter':
        event.preventDefault();
        if (this.selectedSuggestionIndex >= 0 && 
            this.selectedSuggestionIndex < this.autocompleteSuggestions.length) {
          this.selectSuggestion(this.autocompleteSuggestions[this.selectedSuggestionIndex]);
        } else if (this.autocompleteSuggestions.length > 0) {
          // If no suggestion is selected, select the first one
          this.selectSuggestion(this.autocompleteSuggestions[0]);
        }
        break;

      case 'Escape':
        event.preventDefault();
        this.closeAutocomplete();
        break;

      case 'Tab':
        if (this.selectedSuggestionIndex >= 0 && 
            this.selectedSuggestionIndex < this.autocompleteSuggestions.length) {
          event.preventDefault();
          this.selectSuggestion(this.autocompleteSuggestions[this.selectedSuggestionIndex]);
        } else if (this.autocompleteSuggestions.length > 0) {
          event.preventDefault();
          this.selectSuggestion(this.autocompleteSuggestions[0]);
        }
        break;
    }
  }
}

interface HistoryDto {
  id?: number;
  entity?: string;
  text: string;
}

interface HistoryDto {
  id?: number;
  entity?: string;
  text: string;
}
