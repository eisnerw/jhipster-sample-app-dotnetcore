import { Component, OnInit, OnDestroy, Input, ViewChild } from '@angular/core';
import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { ActivatedRoute, ParamMap, Router, Data } from '@angular/router';
import { Subscription, combineLatest, forkJoin } from 'rxjs';
import { take } from 'rxjs/operators';
import { JhiEventManager } from 'ng-jhipster';
import { Directive } from '@angular/core';
import { AbstractControl, NG_ASYNC_VALIDATORS, ValidationErrors, AsyncValidator } from '@angular/forms';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

import { Birthday, IBirthday } from 'app/shared/model/birthday.model';

import { Category, ICategory } from 'app/shared/model/category.model';

import { ITEMS_PER_PAGE } from 'app/shared/constants/pagination.constants';
import { BirthdayService } from '../birthday/birthday.service';
import { CategoryService } from './category.service';
import { RulesetService } from '../ruleset/ruleset.service';
import { catchError, map } from 'rxjs/operators';
import { IStoredRuleset, StoredRuleset } from 'app/shared/model/ruleset.model';

// import { CategoryDeleteDialogComponent } from './category-delete-dialog.component';
import { Observable } from 'rxjs';
import { of } from 'rxjs';
import { Table } from 'primeng/table';
import { MenuItem, MessageService } from 'primeng/api';
import { DomSanitizer } from "@angular/platform-browser";
import { ConfirmationService, PrimeNGConfig} from "primeng/api";
import { faCheck } from '@fortawesome/free-solid-svg-icons';
import { SuperTable } from '../birthday/super-table';
import { BirthdayQueryParserService, IQuery, IQueryRule } from '../birthday/birthday-query-parser.service';
import { BirthdayQueryBuilderComponent } from '../birthday/birthday-query-builder.component';
import { ISelector } from 'app/shared/model/selector.model';
import { Option } from "angular2-query-builder";

interface IView {
  name: string,
  aggregation: string,
  field: string,
  query: string,
  script?: string,
  categoryQuery? : string
  focus? : IBirthday[]
  secondLevelView? : IView
  topLevelView? : IView
  topLevelCategory? : string
  order? : string
}

interface AnalysisMatch
{
    type: string,
    title: string,
    selector: ISelector;
    ids: string[];
}

@Component({
  selector: 'jhi-category',
  templateUrl: './category.component.html',
  providers: [MessageService, ConfirmationService, BirthdayQueryParserService]
})

export class CategoryComponent implements OnInit, OnDestroy {
  rulesetMap: Map<string, IQuery | IQueryRule> = new Map<string, IQuery | IQueryRule>();
  optionsMap: Map<string, Option[]> = new Map<string, Option[]>();
  categories: ICategory[] = [];
  categoriesMap : {} = {};
  eventSubscriber?: Subscription;
  totalItems = 0;
  itemsPerPage = ITEMS_PER_PAGE;
  page!: number;
  predicate!: string;
  ascending!: boolean;
  ngbPaginationPage = 1;
  expandedRows = {};
  displayAsCategories = true;
  faCheck = faCheck;
  categoriesTable: SuperTable | null = null;
  searchQueryAsString = "";
  searchQueryBeforeEdit = "";

  columnDefs = [
    { field: 'categoryName', sortable: true, filter: true },
  ];

  rowData = new Observable<any[]>();

  menuItems: MenuItem[] = [];

  contextSelectedRow: ICategory | null = null;

  checkboxSelectedRows : IBirthday[] = [];

  chipSelectedRows : object[] = [];

  bDisplaySearchDialog = false;

  editingQuery = false;

  bDisplayBirthday = false;

  bDisplayCategories = false;

  birthdayDialogTitle  = "";

  birthdayDialogId : any = "";

  @Input() databaseQuery = "";

  refresh:any = null;

  selectedCategories : ICategory[] = [];

  initialSelectedCategories = "";

  selectedView: IView | null = null ;

  secondLevel = false;

  @ViewChild('queryBuilder') queryBuilder: any;

  @Input() firstColumnIndent = "";

  @Input() hideHeader = false;

  _parent : SuperTable | null = null;

  @Input() get parent(): SuperTable {
    return this._parent as SuperTable;
  }
  set parent(val: SuperTable){
      this._parent = val;
      const parentSelectedView = JSON.parse(JSON.stringify(val.selectedView)); // clone
      this.selectedView = parentSelectedView.secondLevelView;
      delete parentSelectedView.secondLevelView;
      (this.selectedView as IView).topLevelView = parentSelectedView;
  }

  categoryComponent: null | CategoryComponent = null;

  @Input() category : ICategory | null = null;

  @Input() parentComponent: null | CategoryComponent  = null;

  public bRenamingQuery = false;

  public queryToRename = "";

  public newQueryName = "";

  public bDeletingQuery = false;

  public queryToDelete = "";

  public namedQueryUsedIn : string[] = [];

  public storedQueryBeingRenamed! : IQuery;

  public storedQueryBeingDeleted! : IQuery;

  public updatingNamedQueryError = "";

  public deletingNamedQueryError = "";

  public loadingMessage = "";

  views: IView[] = [
    {name:"Category", field: "categories", aggregation: "categories.keyword", query: "categories:*"}
    ,{name:"Birth Year", field: "dob", order: "desc", aggregation: "dob", query: "*", categoryQuery: "dob:[{}-01-01 TO {}-12-31]", script: "\n            String st = doc['dob'].value.getYear().toString();\n            if (st==null){\n              return \"\";\n            } else {\n              return st.substring(0, 4);\n            }\n          "}
    ,{name:"Sign", field: "sign", aggregation: "sign.keyword", query: "sign:*"}
    ,{name: "First Name", field: "fname", aggregation: "fname.keyword", query: "fname:*"}
    ,{name: "Sign/Birth Year", field: "sign", aggregation: "sign.keyword", query: "sign:*", secondLevelView:{name:"Year of Birth", field: "dob", aggregation: "dob", query: "*", categoryQuery: "dob:[{}-01-01 TO {}-12-31]", script: "\n            String st = doc['dob'].value.getYear().toString();\n            if (st==null){\n              return \"\";\n            } else {\n              return st.substring(0, 4);\n            }\n          "}}
    ,{name: "Query", field: "ruleset", aggregation: "", query: "", secondLevelView:{name: "SecondLevel", field: "ruleset2", aggregation: "", query: ""}}
  ];

  public analysisMatches: AnalysisMatch[] = [];


  getLatestRulesets$ : Observable<void> =
    this.rulesetService.query().pipe(take(1), map((res: any): void=> {
      this.rulesetMap.clear();
      let rulesets : IStoredRuleset[] = [];
      rulesets = res.body || [];
      rulesets?.forEach(r=>{
        const query : IQuery = JSON.parse(r.jsonString as string) as IQuery;
        this.rulesetMap.set(r.name as string, this.birthdayQueryParserService.normalize(query, this.rulesetMap as Map<string, IQuery>));
      });
  }));

  getLatestOptions$ : Observable<void> =
    this.birthdayService.postQuery({
      page: 0,
      size: this.itemsPerPage,
      sort: this.sort(),
      query: ""
    }).pipe(take(1), map((res: any) => {
      this.optionsMap.clear();
      let lnames : string[] = [];
      const options : Option[] = [];
      (res.body as IBirthday[]).forEach((b=>{
        if (!lnames.includes(b.lname as string)){
          lnames.push(b.lname as string);
        }
      }));
      lnames = lnames.sort();
      lnames.forEach(n=>{
        options.push({name: n, value: n.toLowerCase()});
      });
      this.optionsMap.set("lname", options);
    },
    () => this.onError()
  ));

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
    protected birthdayQueryParserService : BirthdayQueryParserService,
    private rulesetService: RulesetService
  ) {
    this.refresh = this.refreshData.bind(this);
    this.categoryComponent = this;
  }

  loadPage(page?: number, dontNavigate?: boolean): void {
    const pageToLoad: number = page || this.page || 1;
    const viewQuery: any = this.selectedView === null ? {view: null} : {view:this.selectedView};
    if (this.selectedView && this.selectedView.topLevelView){
      this.selectedView.topLevelCategory = this.category?.notCategorized ? "-" : this.category?.categoryName;
    }
    viewQuery.query = this.databaseQuery;
    this.categoryService
      .postQuery({
        page: pageToLoad - 1,
        size: this.itemsPerPage,
        sort: this.sort(),
        query: JSON.stringify(viewQuery)
      })
      .subscribe(
        (res: any) => this.onSuccess(res.body, res.headers, pageToLoad, !dontNavigate),
        () => this.onError()
      );
  }

  refreshData(): void {
    this.categories =[];
    this.rowData = of(this.categories);
    if (this.categoriesTable){
      this.categoriesTable.children.length = 0;
    }
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

  showSearchDialog(queryBuilder : any) : void {
    forkJoin([this.getLatestOptions$, this.getLatestRulesets$]).subscribe(()=>{
      let queryObject : any = this.birthdayQueryParserService.parse(this.searchQueryAsString, this.rulesetMap, this.optionsMap);
      if (queryObject.Invalid){
        if (this.editingQuery){
          this.searchQueryAsString = this.searchQueryBeforeEdit;
          queryObject = this.birthdayQueryParserService.parse(this.searchQueryAsString, this.rulesetMap, this.optionsMap);
        }
      }
      if (this.searchQueryAsString === ""){
        queryObject = {
          "condition": "and",
          "rules": [
            {
              "field": "document",
              "operator": "contains",
              "value": ""
            }
          ]
        }
      }
      queryBuilder.initialize(JSON.stringify(queryObject));
      this.bDisplaySearchDialog = true;
      this.editingQuery = false;
    });
  }

  cancelSearchDialog() : void {
    this.bDisplaySearchDialog = false;
  }

  editQuery() : void {
    forkJoin([this.getLatestOptions$, this.getLatestRulesets$]).subscribe(()=>{
      this.editingQuery = true;
      this.searchQueryBeforeEdit = this.searchQueryAsString;
    });
  }

  cancelEditQuery() : void{
    this.editingQuery = false;
    this.searchQueryAsString = this.searchQueryBeforeEdit;
  }

  acceptEditQuery() : void{
    if (this.searchQueryAsString.length === 0){
      this.databaseQuery = "";
    } else {
      this.databaseQuery = JSON.stringify(this.birthdayQueryParserService.parse(this.searchQueryAsString, this.rulesetMap, this.optionsMap));
      this.editingQuery = false;
      this.refreshData();
    }
  }

  okSearchDialog(queryBuilder : any, queryEditbox : any) : void {
    if (!queryBuilder.queryIsValid() || queryBuilder.containsDirtyNamedQueries()){
      return;
    }
    if (queryBuilder.query.rules && queryBuilder.query.rules.length === 0){
      this.databaseQuery = "";
      this.searchQueryAsString = "";
    } else {
      this.databaseQuery = JSON.stringify(queryBuilder.query);
      this.birthdayQueryParserService.simplifyQuery(queryBuilder.query as IQuery);
      if (queryBuilder.query.name){
        // top level of the query is named
        this.searchQueryAsString = queryBuilder.query.name;
      } else {
        this.searchQueryAsString = this.birthdayQueryParserService.queryAsString(queryBuilder.query as IQuery);
      }
    }
    this.bDisplaySearchDialog = false;
    const queryObject : any = this.birthdayQueryParserService.parse(this.searchQueryAsString, this.rulesetMap, this.optionsMap);
    if (queryObject.Invalid){
      this.editingQuery = true;
      setTimeout(function() : void{
        // hack needed to turn the box red
        queryEditbox.children[0].children[0].classList.add('ng-dirty');
      }, 0);
    } else {
      this.refreshData();
    }
  }

  clearSearch(): void{
    this.searchQueryAsString = "";
    this.databaseQuery = "";
    this.refreshData();
  }

  setCategoriesTable(categoriesTable: SuperTable): void{
    this.categoriesTable = categoriesTable;
  }

  setViewFocus(focus: IBirthday): void{
    if (this.parentComponent){
      return this.parentComponent.setViewFocus(focus);
    }
    let focusView : IView = {name: 'Focus on ' + (focus.fname as string) + ' ' + (focus.lname as string), field: "", aggregation: "", query: "", focus: [focus], secondLevelView:{name:"focuses", field: "", aggregation: "", query: "", focus: [focus]}};
    this.secondLevel = true;
    if (this.views[this.views.length - 1].focus !== undefined){
      const existingFocus : IView = this.views[this.views.length - 1];
      if ((existingFocus.focus as IBirthday[]).includes(focus)){
        focusView = existingFocus; // the name already exists, so do nothing
      } else {
        focusView.name = existingFocus.name + ' and ' + (focus.fname as string) + ' ' + (focus.lname as string);
        focusView.focus = existingFocus.focus as IBirthday[];
        focusView.focus.push(focus);
      }
      delete this.views[this.views.length - 1];
    }
    this.views.push(focusView);
    const newViews : IView[] = [];
    this.views.forEach(v=>{
      newViews.push(v);
    });
    this.views = newViews;  // replace the list of views so the dropdown sees the change
    this.selectedView = focusView;
    this.refreshData();
  }

  onCheckboxChange() : void {
    this.chipSelectedRows = [];
    if (this.checkboxSelectedRows.length < 3){
      this.checkboxSelectedRows.forEach((row)=>{
        this.chipSelectedRows.push(row);
      });
    } else if (this.checkboxSelectedRows.length < 101) {
      this.chipSelectedRows.push({
        fname: this.checkboxSelectedRows.length,
        lname: 'rows selected',
        id: -1
      })
    }
  }

  onViewChange(event: Event, searchInput: any, categoriesTable : SuperTable): void{
    if (event){
      searchInput.value = ""; // global search must be cleared to prevent odd behavior
      categoriesTable.filter("", "global", "contains"); // reset the global filter
      Object.keys(this.expandedRows).forEach((key)=>{
        this.expandedRows[key] = false; // unexpand all
      });
      if (this.views[this.views.length - 1].focus !== undefined){
        delete this.views[this.views.length - 1]; // get rid of the focus view
      }
      const newViews : IView[] = [];
      this.views.forEach(v=>{
        if (v.name !== "Analysis"){ // Analysis should only appear when displaying result of Analyze
          newViews.push(v);
        }
      });
      this.views = newViews; // replace the list of views so the dropdown sees the change
      this.secondLevel = this.selectedView?.secondLevelView != null;
      this.refreshData();
    }
  }

  setMenu(CategoryOrBirthday : any, bChip: boolean):void{
    if (!bChip && this.selectedView?.name === "Analysis"){
      this.menuItems = [{
        label: 'do something',
        icon: 'pi pi-pencil',
        command: (event: any)=>{
          const menuItem : MenuItem = event.item;
          const categoryComponent = this.parentComponent ? this.parentComponent : this;
          categoryComponent.searchQueryAsString = menuItem.id as string;;
          categoryComponent.showSearchDialog(categoryComponent.queryBuilder);
        }
      }];
    } else if (!bChip && (this.selectedView?.name === "Query" || this.selectedView?.name === "SecondLevel")){
      const category = CategoryOrBirthday as ICategory;
      const query: any= JSON.parse(category.jsonString as string);
      this.menuItems = [{
        label: 'Edit query '+query.name,
        icon: 'pi pi-pencil',
        id: query.name,
        command: (event: any)=>{
          const menuItem : MenuItem = event.item;
          const categoryComponent = this.parentComponent ? this.parentComponent : this;
          categoryComponent.searchQueryAsString = menuItem.id as string;;
          categoryComponent.showSearchDialog(categoryComponent.queryBuilder);
        }
      },{
        label: 'Rename query '+query.name,
        icon: 'pi pi-user-edit',
        command: ()=>{
          this.namedQueryUsedIn = []
          let storedRulesets : IStoredRuleset[] = [];
          this.rulesetService.query().pipe(take(1),map(res  => {
            this.rulesetMap = new Map<string, IQuery | IQueryRule>();
            (storedRulesets = res.body || []);
            storedRulesets.forEach(r=>{
              let q : IQuery = JSON.parse(r.jsonString as string) as IQuery;
              this.rulesetMap.set(r.name as string, this.birthdayQueryParserService.normalize(q, this.rulesetMap as Map<string, IQuery>));
              if ((r.name as string) === query.name){
                this.storedQueryBeingRenamed = this.rulesetMap.get(query.name) as IQuery;
              }
              q = this.rulesetMap.get(r.name as string) as IQuery;
              if (BirthdayQueryBuilderComponent.containsNamedRule(q, query.name as string)){
                this.namedQueryUsedIn.push(r.name as string);
              }
            })
            this.bRenamingQuery = true;
            this.queryToRename = query.name;
            this.newQueryName = "";
          })).subscribe()
        }
      },{
        label: 'Delete query '+query.name,
        icon: 'pi pi-user-edit',
        command: ()=>{
          this.namedQueryUsedIn = []
          let storedRulesets : IStoredRuleset[] = [];
          this.rulesetService.query().pipe(take(1),map(res  => {
            this.rulesetMap = new Map<string, IQuery | IQueryRule>();
            (storedRulesets = res.body || []);
            storedRulesets.forEach(r=>{
              let q : IQuery = JSON.parse(r.jsonString as string) as IQuery;
              this.rulesetMap.set(r.name as string, this.birthdayQueryParserService.normalize(q, this.rulesetMap as Map<string, IQuery>));
              if ((r.name as string) === query.name){
                this.storedQueryBeingDeleted = this.rulesetMap.get(query.name) as IQuery;
              }
              q = this.rulesetMap.get(r.name as string) as IQuery;
              if (BirthdayQueryBuilderComponent.containsNamedRule(q, query.name as string)){
                this.namedQueryUsedIn.push(r.name as string);
              }
            })
            this.bDeletingQuery = true;
            this.queryToDelete = query.name;
          })).subscribe()
        }
      }];
    } else {
      const birthday : IBirthday = CategoryOrBirthday;
      if (!birthday.lname){
        return;
      } else if (birthday.id === -1) {
        this.menuItems = [{
          label: 'Perform analysis on selected documents',
          icon: 'pi pi-bookmark',
          command: ()=>{
            this.analyzeSelected();
          }
        },{
          label: 'Categorize selected documents',
          icon: 'pi pi-bookmark',
          command: ()=>{
            this.categorizeSelected();
          }
        }];
      } else {
        this.menuItems = [{
          label: 'Options',
          items: [
            {
              label: 'Categorize',
              icon: 'pi pi-bookmark',
              command: ()=>{
                setTimeout(()=>{
                  this.selectedCategories.length = 0;
                  const selectedRow = this.contextSelectedRow;
                  this.birthdayDialogId = selectedRow ? selectedRow?.id?.toString() : "";
                  this.birthdayDialogTitle = birthday.fname + " " + birthday.lname;
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
                  this.birthdayDialogTitle = birthday.lname as string;
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
          },
          {
              label: 'Analysis',
              items: [{
                label: 'Perform analysis on selected documents',
                icon: 'pi pi-bookmark',
                command: ()=>{
                  this.analyzeSelected();
                }
              }]
          }
        ];
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
    }
  }

  categorizeSelected(): void{
    const idQuery = {
      ids: [] as string[]
    };
    this.checkboxSelectedRows.forEach(r=>{
      idQuery.ids.push(r.id as unknown as string);
    });
    this.birthdayService
    .postQuery({
      page: 0,
      size: this.itemsPerPage,
      sort: this.sort(),
      query: JSON.stringify(idQuery)
    })
    .subscribe(
      (resSelected: any) => {
        const selectedRows = resSelected.body as IBirthday[];
        const firstCategories : ICategory[] = this.checkboxSelectedRows[0].categories === null ? [] : this.checkboxSelectedRows[0].categories as ICategory[];
        let categoryNames : string[] = [];
        firstCategories.forEach(cat=>{
          categoryNames.push(cat.categoryName as string);
        });
        selectedRows.forEach(r=>{
          if (categoryNames.length === 0 || r.categories === null || r.categories?.length === 0){
            categoryNames = [];
          } else {
            const refinedCategoryNames : string[] = [];
            r.categories?.forEach(c=>{
              if (categoryNames.includes(c.categoryName as string) && !refinedCategoryNames.includes(c.categoryName as string)){
                refinedCategoryNames.push(c.categoryName as string);
              }
            });
            categoryNames = refinedCategoryNames;
          }
          this.selectedCategories.length = 0;
          this.birthdayDialogTitle = "multiple";
          this.categoryService
          .postQuery({
            page: 0,
            size: 10000,
            sort: this.sortCategory(),
            query: selectedRows[0].id // this request requires an id
          })
          .subscribe(
            (res: any) => {
              const retrievedCategories : ICategory[] = res.body as ICategory[];
              this.selectedCategories = [];
              this.categories = [];

              retrievedCategories.forEach(rc=>{
                this.categories.push(rc);
                if (categoryNames.includes(rc.categoryName as string)){
                  rc.selected = true;
                  this.selectedCategories.push(rc);
                } else {
                  rc.selected = false;
                }
              });
              this.initialSelectedCategories = categoryNames.join(",");
              this.bDisplayCategories = true;
              this.contextSelectedRow = new Birthday();
              this.contextSelectedRow.ids = idQuery.ids;
            },
            () => this.onError()
          );
        });

      },
      () => this.onError()
    ); 
  }

  analyzeSelected():void{
    const ids : string[] = [];
    this.checkboxSelectedRows.forEach(r=>{
      ids.push(r.id as unknown as string);
    });
    this.categoryService.analyze({
      ids
    }).subscribe((r) => {
      if (!r.ok){
        alert("Analysis failed");
      } else {
        this.analysisMatches = (r as any).body.matches;
        const analysisView : IView = {name: "Analysis", field: "", aggregation: "", query: "", secondLevelView:{name: "SecondLevelAnalysis", field: "analysis", aggregation: "", query: ""}};
        this.secondLevel = true;
        if (this.views[this.views.length - 1].name !== "Analysis"){
          this.views.push(analysisView);
          const newViews : IView[] = [];
          this.views.forEach(v=>{
            newViews.push(v);
          });
          this.views = newViews;  // replace the list of views so the dropdown sees the change
        }
        this.selectedView = analysisView;
        this.categories = [];
        let category = "";
        let categoryId = 0;
        const usedTypes : string[] = [];
        this.analysisMatches.forEach(m=>{
          if (!usedTypes.includes(m.type)){
            usedTypes.push(m.type);
            switch (m.type){
              case"none":
                category = "Documents that matched no selectors";
                break;

              case"single":
                category = "Documents that matched one selector";
                break;

              case"multiple":
                category = "Documents that matched multiple selectors";
                break;
            }
            this.categories.push(new Category(categoryId, category));
          }
          categoryId++;
        });
        this.rowData = of(this.categories);
        this.displayAsCategories = true;
      }
    });
  }

  cancelDeleteQuery():void{
    this.bDeletingQuery = false;
  }

  okDeleteQuery():void{
    if (this.editingQuery){
      this.cancelEditQuery();
    }
    const topLevel = this.parentComponent ? this.parentComponent : this;
    this.deletingNamedQueryError = "";
    const name = this.storedQueryBeingDeleted?.name as string;
    this.namedQueryUsedIn.forEach(q=>{
      this.removeNamedQueryName(this.rulesetMap.get(q) as IQuery, name);
    });
    let jsonString = JSON.stringify(this.storedQueryBeingDeleted);
    let storedRuleset = new StoredRuleset(undefined, name, jsonString);
    storedRuleset.bDelete = true;
    const updateSuccess = ()  => {
      if (this.namedQueryUsedIn.length > 0){
        const namedQueryToBeUpdated = this.rulesetMap.get(this.namedQueryUsedIn.pop() as string) as IQuery;
        jsonString = JSON.stringify(namedQueryToBeUpdated);
        storedRuleset = new StoredRuleset(undefined, namedQueryToBeUpdated.name, jsonString);
        this.rulesetService.update(storedRuleset).pipe(take(1), map(updateSuccess),catchError(updateError)).subscribe();
      } else {
        // all done
        this.rulesetMap.delete(name);
        this.bDeletingQuery = false;
        topLevel.refreshData();
      }
    };
    const updateError = (error: any) => {
      // server error from the update
      this.updatingNamedQueryError = error.error?.detail;
      return of([]);
    };
    this.rulesetService.update(storedRuleset).pipe(take(1), map(updateSuccess), catchError(updateError)).subscribe();
  }

  removeNamedQueryName(query : IQuery, name: string) : void {
    if (!query.rules){
      return;
    }
    const newRules : IQuery[] = [];
    query.rules.forEach(r=>{
      this.removeNamedQueryName(r as unknown as IQuery, name);
      if ((r as any).name === name){
        const clone = JSON.parse(JSON.stringify(r));
        delete clone.name;
        newRules.push(this.birthdayQueryParserService.normalize(clone, this.rulesetMap as Map<string, IQuery>));
      } else {
        newRules.push(r as unknown as IQuery);
      }
    });
    query.rules = newRules as any;
  }

  cancelRenameQuery():void{
    this.bRenamingQuery = false;
  }

  okRenameQuery():void{
    if (this.editingQuery){
      this.cancelEditQuery();
    }
    let queryBeingEdited : any = null;
    const topLevel = this.parentComponent ? this.parentComponent : this;
    if (topLevel.searchQueryAsString !== ""){
      // capture the query in the editor which could get renamed
      queryBeingEdited = this.birthdayQueryParserService.parse(topLevel.searchQueryAsString, this.rulesetMap, this.optionsMap);
      queryBeingEdited = this.birthdayQueryParserService.normalize(queryBeingEdited, this.rulesetMap as Map<string, IQuery>);
    }
    this.updatingNamedQueryError = "";
    const oldname = this.storedQueryBeingRenamed?.name as string;
    this.storedQueryBeingRenamed.name = this.newQueryName;
    let jsonString = JSON.stringify(this.storedQueryBeingRenamed);
    let storedRuleset = new StoredRuleset(undefined, oldname, jsonString); // note: jsonString has the new name, which will be detected by the server
    const updateSuccess = ()  => {
      if (this.namedQueryUsedIn.length > 0){
        const namedQueryToBeUpdated = this.rulesetMap.get(this.namedQueryUsedIn.pop() as string) as IQuery;
        jsonString = JSON.stringify(namedQueryToBeUpdated);
        storedRuleset = new StoredRuleset(undefined, namedQueryToBeUpdated.name, jsonString);
        this.rulesetService.update(storedRuleset).pipe(take(1), map(updateSuccess),catchError(updateError)).subscribe();
      } else {
        // all done
        this.rulesetMap.set(this.newQueryName, this.rulesetMap.get(oldname) as IQueryRule);
        this.rulesetMap.delete(oldname);
        if (queryBeingEdited !== null){
          topLevel.searchQueryAsString = queryBeingEdited.name ? queryBeingEdited.name : this.birthdayQueryParserService.queryAsString(queryBeingEdited);
        }
        this.bRenamingQuery = false;
        topLevel.refreshData();
      }
    };
    const updateError = (error: any) => {
      // server error from the update
      this.updatingNamedQueryError = error.error?.detail;
      return of([]);
    };
    this.rulesetService.update(storedRuleset).pipe(take(1), map(updateSuccess), catchError(updateError)).subscribe();
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
    if (!expanded){
      setTimeout(()=>{
        // remove any collapsed (non-connected) children.  Expansion will add them back in
        this.categoriesTable?.removeInvisibleChildren();
      },0); // seems to require some time for elastic to catch up
    }
  }

  onRemoveChip(chip : any) : void {
    if (chip.id === -1){
      this.checkboxSelectedRows = [];
      return;
    }
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
    const selectedCategoryNames : string[] = [];
    this.selectedCategories.forEach(s=>{
      selectedCategoryNames.push(s.categoryName as string);
    });
    if (selectedCategoryNames.join(",") !== this.initialSelectedCategories){
      const selectedCategories : ICategory[] = [];
      this.categories.forEach(c=>{
        // single category passes select or not; multiple categories passes selected
        if ((!c.selected || this.contextSelectedRow?.ids != null) && selectedCategoryNames.includes(c.categoryName as string)){
          c.selected = true;
          selectedCategories.push(c);
        } else if (c.selected && this.contextSelectedRow?.ids == null && !selectedCategoryNames.includes(c.categoryName as string)){
          c.selected = false;
          selectedCategories.push(c);
        }
      });
      (this.contextSelectedRow as IBirthday).categories = selectedCategories;
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
    this.handleNavigation();
    this.registerChangeInCategories();
    this.primeNGConfig.ripple = true;
    window.onresize = () =>{
      this.refreshData();
    }
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
        if (this.selectedView?.name === "SecondLevelAnalysis"){
          // special analysis view
          const categoryType = this.parentComponent?.analysisMatches[this.category?.id as number].type;
          this.categories = [];
          let categoryId = 0;
          this.parentComponent?.analysisMatches.forEach(m=>{
            if (m.type === categoryType){
              let title = m.title;
              if (m.type === "single"){
                title = "Matched selector '" + m.title + "'";
              } else if (m.type === "multiple"){
                title = "Matched selectors " + m.title;
              }
              const category = new Category(categoryId++, title);
              category.ids = m.ids;
              this.categories.push(category);
            }
          });
          this.rowData = of(this.categories);
        } else {
          this.loadPage(pageNumber, true);
        }
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
    this.ngbPaginationPage = this.page;

    if (data) {
      const loadIncrement = 50;
      const loadData : any[] = this.categories?.slice(0, loadIncrement);
      let loaded = loadIncrement;
      const rowLoader = ()=>{
        loadData.splice(loaded, 0, ...(this.categories as any[]).slice(loaded, loaded + loadIncrement));
        loaded += loadIncrement;
        const limitData = 2000;
        if (loadData.length > limitData){
          this.loadingMessage = this.categories?.length + " hits (too many to display, showing the first " + limitData +")";
          loadData.length = limitData;
          this.rowData = of(loadData);
          this.runFilter()
          return;
        }
        this.rowData = of(loadData);
        if (loaded < (this.categories as any[]).length){
          this.loadingMessage = "Loading " + loaded + "..."
          setTimeout(rowLoader, 10);
        } else {
          this.loadingMessage = "";
          this.runFilter();
        }
      }
      this.rowData = of(this.categories.slice(0,loadIncrement));
      const hasCategories = this.categories?.length !== 1 || this.parentComponent != null;
      this.displayAsCategories = hasCategories || !!this.views[this.views.length - 1].focus || (this.selectedView !== null && this.selectedView.field.startsWith("ruleset"));
      this.loadingMessage = "Loading...";
      setTimeout(rowLoader, 10);
    }
  }

  private runFilter() : void {
    if (this.categoriesTable != null){
      const categoriesTable = this.categoriesTable;
      setTimeout(function() : void{
        if (categoriesTable.displayingAsCategories){
          categoriesTable.filteringGlobal = true;
        }
        categoriesTable._filter();
      }, 0);
    }
  }

  protected onError(): void {
    this.ngbPaginationPage = this.page ?? 1;
  }
}

@Directive({
  selector: '[jhiValidateRulesetRename]',
  providers: [{provide: NG_ASYNC_VALIDATORS, useExisting: RulesetRenameValidatorDirective, multi: true}]
})

export class RulesetRenameValidatorDirective implements AsyncValidator {
  storedRulesets : IStoredRuleset[] = [];
  constructor(private rulesetService: RulesetService ) {}

  validate(control: AbstractControl): Observable<ValidationErrors | null> {
      const obs = this.rulesetService.query().pipe(map(res  => {
        (this.storedRulesets = res.body || []);
        let bFound = false;
        this.storedRulesets.forEach(r =>{
          if (r.name === control.value){
            bFound = true;
          }
        });
        if (bFound){
          return {
            error: "Name already used"
          }
        }
        if (/^[A-Z][A-Z_\d]*$/.test(control.value)){
          return null;
        }
        return {
          error: "Invalid name"
        };
      }));
      return obs;
  }
}
