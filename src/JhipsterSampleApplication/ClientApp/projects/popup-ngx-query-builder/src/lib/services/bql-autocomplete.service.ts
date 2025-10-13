import { Injectable } from '@angular/core';
import { QueryBuilderConfig } from 'ngx-query-builder';
import { tokenize, Token } from '../bql';

/**
 * BQL Autocomplete Service
 * 
 * This service provides intelligent autocomplete suggestions for BQL queries based on
 * the QueryBuilderConfig. It integrates with the existing query builder configuration:
 * 
 * - Respects field.operators configuration (or falls back to type-based defaults)
 * - Uses field.options for predefined value suggestions
 * - Calls field.categorySource for dynamic value suggestions
 * - Handles field.nullable by adding "is null" / "is not null" operators
 * - Supports config.getOperators for dynamic operator determination
 * - Supports config.getOptions for dynamic option retrieval
 * - Excludes computed fields from field suggestions (type: 'computed')
 * 
 * Note: field.validator is not used for filtering suggestions. Validators are applied
 * during query validation after the user enters values, not during autocomplete.
 */

export interface AutocompleteSuggestion {
  value: string;           // The text to insert
  display: string;         // The text to show in dropdown
  type: 'field' | 'operator' | 'value' | 'keyword';
  metadata?: {
    fieldType?: string;    // For fields: string, number, date, etc.
    description?: string;  // Additional info to display
  };
}

export interface AutocompleteContext {
  type: 'field' | 'operator' | 'value' | 'unknown';
  currentField?: string;   // If in operator/value context
  currentOperator?: string; // If in value context
  prefix: string;          // Text typed so far in current token
  cursorPosition: number;
}

@Injectable()
export class BqlAutocompleteService {
  // Cache for field and operator suggestions per QueryBuilderConfig
  private fieldSuggestionsCache = new Map<string, AutocompleteSuggestion[]>();
  private operatorSuggestionsCache = new Map<string, Map<string, AutocompleteSuggestion[]>>();
  
  // Maximum number of suggestions to display
  private readonly MAX_SUGGESTIONS = 15;
  
  constructor() {}

  /**
   * Analyzes the query and cursor position to determine what suggestions to show
   */
  getSuggestions(
    query: string,
    cursorPosition: number,
    config: QueryBuilderConfig
  ): AutocompleteSuggestion[] {
    try {
      // Handle missing or incomplete QueryLanguageSpec gracefully
      if (!config) {
        console.warn('BqlAutocompleteService: QueryBuilderConfig is missing, providing minimal suggestions');
        return this.limitSuggestions(this.getMinimalSuggestions());
      }

      if (!config.fields || Object.keys(config.fields).length === 0) {
        console.warn('BqlAutocompleteService: QueryBuilderConfig.fields is missing or empty, providing minimal suggestions');
        return this.limitSuggestions(this.getMinimalSuggestions());
      }

      // Check if cursor is inside a quoted string or regex pattern
      // If so, don't provide autocomplete suggestions (user is typing free text)
      if (this.isCursorInQuotedStringOrRegex(query, cursorPosition)) {
        return [];
      }

      const context = this.analyzeContext(query, cursorPosition, config);
      
      let suggestions: AutocompleteSuggestion[] = [];
      
      switch (context.type) {
        case 'field':
          suggestions = this.getFieldSuggestions(context.prefix, config);
          break;
        case 'operator':
          if (context.currentField) {
            suggestions = this.getOperatorSuggestions(context.currentField, context.prefix, config);
          }
          break;
        case 'value':
          if (context.currentField && context.currentOperator) {
            suggestions = this.getValueSuggestions(
              context.currentField,
              context.currentOperator,
              context.prefix,
              config
            );
          }
          break;
      }
      
      const filtered = this.filterSuggestions(suggestions, context.prefix);
      return this.limitSuggestions(filtered);
    } catch (error) {
      // Log error but don't break functionality
      console.error('BqlAutocompleteService: Error generating suggestions', error);
      return [];
    }
  }
  
  /**
   * Clears the suggestion cache
   * Call this when the QueryBuilderConfig changes
   */
  clearCache(): void {
    this.fieldSuggestionsCache.clear();
    this.operatorSuggestionsCache.clear();
  }

  /**
   * Determines the current context (field, operator, or value)
   */
  private analyzeContext(
    query: string,
    cursorPosition: number,
    config: QueryBuilderConfig
  ): AutocompleteContext {
    // Handle empty query - suggest fields
    if (!query || query.trim() === '') {
      return {
        type: 'field',
        prefix: '',
        cursorPosition
      };
    }

    // Get the portion of the query up to the cursor
    const queryBeforeCursor = query.substring(0, cursorPosition);
    
    try {
      // Tokenize the query up to cursor position
      const tokens = tokenize(queryBeforeCursor);
      
      // If no tokens, suggest fields
      if (tokens.length === 0) {
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }

      // Get the last token
      const lastToken = tokens[tokens.length - 1];
      
      // Check if cursor is at the start of the query
      if (cursorPosition === 0 || queryBeforeCursor.trim() === '') {
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }

      // Check if the last character before cursor is a logical operator or opening parenthesis
      const trimmedBefore = queryBeforeCursor.trimEnd();
      const lastChar = trimmedBefore[trimmedBefore.length - 1];
      
      if (lastChar === '&' || lastChar === '|') {
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }
      
      // Special handling for comma - check if it's inside IN/!IN
      if (lastChar === ',') {
        // Check if we're inside an IN or !IN operator
        const inMatch = /\b(\w+)\s+(!?IN)\s*\([^)]*$/i.exec(trimmedBefore);
        
        if (inMatch) {
          // We're inside IN(...) or !IN(...) after a comma - this is a value context
          const fieldName = inMatch[1];
          const operator = inMatch[2];
          return {
            type: 'value',
            currentField: fieldName,
            currentOperator: operator,
            prefix: '',
            cursorPosition
          };
        }
      }
      
      // Special handling for opening parenthesis - check if it's part of IN/!IN
      if (lastChar === '(') {
        // Check if this is an IN or !IN operator
        const beforeParen = trimmedBefore.substring(0, trimmedBefore.length - 1).trim();
        const inMatch = /\b(\w+)\s+(!?IN)\s*$/i.exec(beforeParen);
        
        if (inMatch) {
          // We're inside IN(...) or !IN(...) - this is a value context
          const fieldName = inMatch[1];
          const operator = inMatch[2];
          return {
            type: 'value',
            currentField: fieldName,
            currentOperator: operator,
            prefix: '',
            cursorPosition
          };
        }
        
        // Regular parenthesis for grouping - suggest fields
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }

      // Check if we're after a standalone negation operator (for negating conditions)
      // But NOT if it's after a field name (which would be the start of an operator like !IN)
      if (lastChar === '!' && tokens.length > 0 && tokens[tokens.length - 1].value === '!') {
        // Check if there's a field name before the !
        if (tokens.length >= 2) {
          const tokenBeforeExclamation = tokens[tokens.length - 2];
          if (tokenBeforeExclamation.type === 'word' && config.fields[tokenBeforeExclamation.value]) {
            // This is "field !" - user is starting to type an operator like !IN
            return {
              type: 'operator',
              currentField: tokenBeforeExclamation.value,
              prefix: '!',
              cursorPosition
            };
          }
        }
        
        // Standalone ! for negation - suggest fields
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }

      // Check if we're typing an operator after a field name
      // Pattern: "fieldname partial_operator" where partial_operator could be "!", "!c", "con", etc.
      const operatorMatch = /\b(\w+)\s+([!a-z]\w*)$/i.exec(queryBeforeCursor);
      if (operatorMatch) {
        const potentialField = operatorMatch[1];
        const partialOperator = operatorMatch[2];
        
        // Check if it's a valid field name
        if (config.fields[potentialField]) {
          // This looks like "field partial_operator" - suggest operators
          return {
            type: 'operator',
            currentField: potentialField,
            prefix: partialOperator,
            cursorPosition
          };
        }
      }
      
      // Before analyzing parentheses, check if we're typing inside an IN/!IN context
      // This catches cases like "sign IN (Gemini,a" where 'a' is being typed
      const inContextMatch = /\b(\w+)\s+(!?IN)\s*\(([^)]*)$/i.exec(queryBeforeCursor);
      if (inContextMatch) {
        const fieldName = inContextMatch[1];
        const operator = inContextMatch[2];
        const contentInsideParens = inContextMatch[3];
        
        // Extract prefix after last comma (or all content if no comma)
        const lastCommaPos = contentInsideParens.lastIndexOf(',');
        const prefix = lastCommaPos >= 0 
          ? contentInsideParens.substring(lastCommaPos + 1).trim()
          : contentInsideParens.trim();
        
        return {
          type: 'value',
          currentField: fieldName,
          currentOperator: operator,
          prefix: prefix,
          cursorPosition
        };
      }

      // Handle parentheses context - find the most recent unmatched opening parenthesis
      // This helps maintain proper context through nested parentheses
      const parenContext = this.analyzeParenthesesContext(tokens);
      if (parenContext.insideParentheses) {
        // Check if the opening paren is part of an IN or !IN operator
        const parenIndex = parenContext.lastOpenParenIndex;
        if (parenIndex > 0) {
          const tokenBeforeParen = tokens[parenIndex - 1];
          if (tokenBeforeParen && tokenBeforeParen.type === 'operator') {
            const opLower = tokenBeforeParen.value.toLowerCase();
            if (opLower === 'in' || opLower === '!in') {
              // We're inside IN(...) or !IN(...) - find the field name
              if (parenIndex > 1) {
                const fieldToken = tokens[parenIndex - 2];
                if (fieldToken && fieldToken.type === 'word') {
                  // Get the current prefix (text after last comma or after opening paren)
                  const tokensAfterParen = tokens.slice(parenIndex + 1);
                  let prefix = '';
                  
                  // Find the last comma in the tokens after paren
                  let lastCommaIndex = -1;
                  for (let i = tokensAfterParen.length - 1; i >= 0; i--) {
                    if (tokensAfterParen[i].type === 'symbol' && tokensAfterParen[i].value === ',') {
                      lastCommaIndex = i;
                      break;
                    }
                  }
                  
                  // If there's a comma, get text after it; otherwise get all text after paren
                  if (lastCommaIndex >= 0 && lastCommaIndex < tokensAfterParen.length - 1) {
                    const lastToken = tokensAfterParen[tokensAfterParen.length - 1];
                    prefix = lastToken.value || '';
                  } else if (tokensAfterParen.length > 0) {
                    const lastToken = tokensAfterParen[tokensAfterParen.length - 1];
                    prefix = lastToken.value || '';
                  }
                  
                  return {
                    type: 'value',
                    currentField: fieldToken.value,
                    currentOperator: tokenBeforeParen.value,
                    prefix: prefix,
                    cursorPosition
                  };
                }
              }
            }
          }
        }
        
        // We're inside parentheses, analyze context from the last opening paren
        const tokensAfterParen = tokens.slice(parenContext.lastOpenParenIndex + 1);
        
        // If no tokens after the opening paren, suggest fields
        if (tokensAfterParen.length === 0) {
          return {
            type: 'field',
            prefix: '',
            cursorPosition
          };
        }
        
        // Analyze the tokens after the opening paren to determine context
        return this.analyzeTokenSequence(tokensAfterParen, config, cursorPosition);
      }

      // If not inside parentheses, analyze the full token sequence
      return this.analyzeTokenSequence(tokens, config, cursorPosition);

    } catch (error) {
      // Handle tokenization errors in malformed queries
      // This is expected during typing (e.g., unterminated strings, incomplete operators)
      // Continue providing suggestions even when validateBql would return false
      
      // Try to extract a partial prefix from the query before cursor
      const trimmedQuery = queryBeforeCursor.trim();
      
      // Check if we can at least determine we're after a logical operator
      if (trimmedQuery.endsWith('&') || trimmedQuery.endsWith('|') || trimmedQuery.endsWith('(')) {
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }
      
      // Try to extract the last word as a potential prefix
      const lastSpaceIndex = Math.max(
        trimmedQuery.lastIndexOf(' '),
        trimmedQuery.lastIndexOf('&'),
        trimmedQuery.lastIndexOf('|'),
        trimmedQuery.lastIndexOf('(')
      );
      
      const potentialPrefix = lastSpaceIndex >= 0 
        ? trimmedQuery.substring(lastSpaceIndex + 1).trim()
        : trimmedQuery;
      
      // Default to field suggestions with the extracted prefix
      return {
        type: 'field',
        prefix: potentialPrefix,
        cursorPosition
      };
    }

    // Default fallback
    return {
      type: 'unknown',
      prefix: '',
      cursorPosition
    };
  }

  /**
   * Provides minimal suggestions when config is not available
   * Returns basic BQL operators that work without field configuration
   */
  private getMinimalSuggestions(): AutocompleteSuggestion[] {
    return [
      {
        value: 'EXISTS',
        display: 'EXISTS (field exists)',
        type: 'operator',
        metadata: {
          description: 'Check if field exists'
        }
      },
      {
        value: '!EXISTS',
        display: '!EXISTS (field does not exist)',
        type: 'operator',
        metadata: {
          description: 'Check if field does not exist'
        }
      }
    ];
  }

  /**
   * Generates field name suggestions
   * Results are cached per QueryBuilderConfig
   */
  private getFieldSuggestions(
    prefix: string,
    config: QueryBuilderConfig
  ): AutocompleteSuggestion[] {
    if (!config.fields) {
      return [];
    }

    // Generate cache key based on field configuration
    const cacheKey = this.generateConfigCacheKey(config);
    
    // Check cache first
    if (this.fieldSuggestionsCache.has(cacheKey)) {
      return this.fieldSuggestionsCache.get(cacheKey)!;
    }

    const suggestions: AutocompleteSuggestion[] = [];

    try {
      Object.keys(config.fields).forEach(fieldKey => {
        const field = config.fields[fieldKey];
        
        // Handle incomplete field configuration gracefully
        if (!field) {
          console.warn(`BqlAutocompleteService: Field configuration missing for key: ${fieldKey}`);
          return;
        }
        
        // Skip computed fields - they are derived from other fields and not directly queryable
        if (field.type === 'computed') {
          return;
        }
        
        // Only show description if it's meaningfully different (not just case)
        const isDifferent = field.name && field.name.toLowerCase() !== fieldKey.toLowerCase();
        
        suggestions.push({
          value: fieldKey,
          display: fieldKey,
          type: 'field',
          metadata: {
            description: isDifferent ? field.name : undefined
          }
        });
      });

      // Sort alphabetically by display name
      suggestions.sort((a, b) => a.display.localeCompare(b.display));
      
      // Cache the results
      this.fieldSuggestionsCache.set(cacheKey, suggestions);
    } catch (error) {
      console.error('BqlAutocompleteService: Error generating field suggestions', error);
    }

    return suggestions;
  }

  /**
   * Generates operator suggestions for a specific field
   * Results are cached per field and QueryBuilderConfig
   */
  private getOperatorSuggestions(
    fieldName: string,
    prefix: string,
    config: QueryBuilderConfig
  ): AutocompleteSuggestion[] {
    try {
      const fieldConf = config.fields[fieldName];
      if (!fieldConf) {
        console.warn(`BqlAutocompleteService: Field configuration not found for: ${fieldName}`);
        return [];
      }

      // Generate cache key based on field configuration
      const configCacheKey = this.generateConfigCacheKey(config);
      
      // Check cache first
      if (!this.operatorSuggestionsCache.has(configCacheKey)) {
        this.operatorSuggestionsCache.set(configCacheKey, new Map());
      }
      
      const fieldCache = this.operatorSuggestionsCache.get(configCacheKey)!;
      if (fieldCache.has(fieldName)) {
        return fieldCache.get(fieldName)!;
      }

      const suggestions: AutocompleteSuggestion[] = [];

      // Get allowed operators for this field
      let operators: string[] = [];
      
      // Respect field.operators configuration if provided
      if (fieldConf.operators && fieldConf.operators.length > 0) {
        operators = [...fieldConf.operators];
      } else {
        // Fall back to default operators based on field type if not specified
        const fieldType = fieldConf.type || 'string';
        operators = this.getDefaultOperatorsForFieldType(fieldType);
      }
      
      // Use config.getOperators if available (allows dynamic operator determination)
      if (config.getOperators) {
        try {
          const dynamicOperators = config.getOperators(fieldName, fieldConf);
          if (dynamicOperators && dynamicOperators.length > 0) {
            operators = dynamicOperators;
          }
        } catch (error) {
          console.warn(`BqlAutocompleteService: Error calling config.getOperators for field ${fieldName}:`, error);
          // Continue with existing operators
        }
      }

      // Add nullable operators if field is nullable
      if (fieldConf.nullable) {
        if (!operators.includes('is null')) {
          operators.push('is null');
        }
        if (!operators.includes('is not null')) {
          operators.push('is not null');
        }
      }

      // Map operators to readable names
      const operatorMap: Record<string, string> = {
        '=': 'equals',
        '!=': 'not equals',
        '>': 'greater than',
        '>=': 'greater than or equal',
        '<': 'less than',
        '<=': 'less than or equal',
        'contains': 'contains',
        '!contains': 'does not contain',
        'like': 'matches pattern',
        '!like': 'does not match pattern',
        'in': 'in list',
        '!in': 'not in list',
        'exists': 'exists',
        '!exists': 'does not exist',
        'is null': 'is null',
        'is not null': 'is not null'
      };

      operators.forEach(op => {
        const displayName = operatorMap[op] || op;
        // Convert word operators to UPPERCASE for BQL syntax
        // Symbol operators (=, !=, <, >, <=, >=) stay as-is
        const bqlOperator = /^[a-z!]/.test(op) ? op.toUpperCase() : op;
        
        // For negated operators, show description in parentheses
        // For others, just show the operator
        const isNegated = bqlOperator.startsWith('!') || bqlOperator.startsWith('IS NOT');
        const displayText = isNegated ? `${bqlOperator} (${displayName})` : bqlOperator;
        
        suggestions.push({
          value: bqlOperator,
          display: displayText,
          type: 'operator'
          // No metadata.description for operators - it's already in display
        });
      });
      
      // Cache the results
      fieldCache.set(fieldName, suggestions);
      
      return suggestions;
    } catch (error) {
      console.error(`BqlAutocompleteService: Error generating operator suggestions for field ${fieldName}:`, error);
      return [];
    }
  }

  /**
   * Returns default operators for a given field type
   */
  private getDefaultOperatorsForFieldType(fieldType: string): string[] {
    switch (fieldType) {
      case 'string':
        return ['=', '!=', 'contains', '!contains', 'like', '!like', 'in', '!in', 'exists'];
      case 'number':
        return ['=', '!=', '>', '>=', '<', '<=', 'in', '!in', 'exists'];
      case 'date':
        return ['=', '!=', '>', '>=', '<', '<=', 'exists'];
      case 'boolean':
        return ['=', '!=', 'exists'];
      case 'category':
        return ['=', '!=', 'in', '!in', 'exists'];
      default:
        // Generic fallback
        return ['=', '!=', 'exists'];
    }
  }

  /**
   * Generates value suggestions for a field/operator combination
   */
  private getValueSuggestions(
    fieldName: string,
    operator: string,
    prefix: string,
    config: QueryBuilderConfig
  ): AutocompleteSuggestion[] {
    const suggestions: AutocompleteSuggestion[] = [];
    
    try {
      const fieldConf = config.fields[fieldName];
      if (!fieldConf) {
        console.warn(`BqlAutocompleteService: Field configuration not found for: ${fieldName}`);
        return suggestions;
      }

      // Handle "in" and "!in" operators for multi-value context
      // Extract the current prefix after the last comma for multi-value operators
      let actualPrefix = prefix;
      const opLower = operator.toLowerCase();
      const isMultiValueOperator = opLower === 'in' || opLower === '!in';
      
      if (isMultiValueOperator && prefix.includes(',')) {
        // Get the text after the last comma
        const lastCommaIndex = prefix.lastIndexOf(',');
        actualPrefix = prefix.substring(lastCommaIndex + 1).trim();
      }

      // Handle boolean fields
      if (fieldConf.type === 'boolean') {
        suggestions.push(
          {
            value: 'true',
            display: 'true',
            type: 'value'
          },
          {
            value: 'false',
            display: 'false',
            type: 'value'
          }
        );
        return suggestions;
      }

      // Try config.getOptions first if available (allows dynamic option determination)
      if (config.getOptions) {
        try {
          const dynamicOptions = config.getOptions(fieldName);
          if (dynamicOptions && dynamicOptions.length > 0) {
            dynamicOptions.forEach(option => {
              if (option && option.value !== undefined) {
                suggestions.push({
                  value: String(option.value),
                  display: option.name || String(option.value),
                  type: 'value',
                  metadata: {
                    description: option.name !== String(option.value) ? String(option.value) : undefined
                  }
                });
              }
            });
            return suggestions;
          }
        } catch (error) {
          console.warn(`BqlAutocompleteService: Error calling config.getOptions for field ${fieldName}:`, error);
          // Continue to try other methods
        }
      }

      // Handle fields with predefined options
      if (fieldConf.options && fieldConf.options.length > 0) {
        try {
          fieldConf.options.forEach(option => {
            if (option && option.value !== undefined) {
              suggestions.push({
                value: String(option.value),
                display: option.name || String(option.value),
                type: 'value',
                metadata: {
                  description: option.name !== String(option.value) ? String(option.value) : undefined
                }
              });
            }
          });
          return suggestions;
        } catch (error) {
          console.warn(`BqlAutocompleteService: Error processing field options for ${fieldName}:`, error);
          return suggestions;
        }
      }

      // Handle fields with categorySource for dynamic value suggestions
      if (fieldConf.categorySource) {
        try {
          // Create a dummy rule for categorySource
          const dummyRule = { field: fieldName, operator, value: null };
          const dummyParent = { condition: 'and' as const, rules: [] };
          const categories = fieldConf.categorySource(dummyRule, dummyParent);
          
          if (categories && Array.isArray(categories)) {
            categories.forEach(cat => {
              if (cat !== null && cat !== undefined) {
                suggestions.push({
                  value: String(cat),
                  display: String(cat),
                  type: 'value'
                });
              }
            });
          }
        } catch (error) {
          console.warn(`BqlAutocompleteService: Error calling categorySource for field ${fieldName}:`, error);
          // Continue without categorySource suggestions
        }
        return suggestions;
      }

      // For string, number, and date fields without options, return empty
      // (user will type free text)
      // Note: field.validator is not used here as it's for validation after value entry,
      // not for filtering suggestions. Validators are applied when the user submits the query.
    } catch (error) {
      console.error(`BqlAutocompleteService: Error generating value suggestions for field ${fieldName}:`, error);
    }

    return suggestions;
  }

  /**
   * Filters suggestions based on prefix match (case-insensitive)
   * Sorts results to prioritize matches that start with the prefix
   */
  private filterSuggestions(
    suggestions: AutocompleteSuggestion[],
    prefix: string
  ): AutocompleteSuggestion[] {
    try {
      if (!suggestions || suggestions.length === 0) {
        return [];
      }

      if (!prefix || prefix.trim() === '') {
        return suggestions;
      }

      const lowerPrefix = prefix.toLowerCase();
      
      const filtered = suggestions.filter(suggestion => {
        try {
          // Match against both value and display
          return (
            suggestion.value.toLowerCase().includes(lowerPrefix) ||
            suggestion.display.toLowerCase().includes(lowerPrefix)
          );
        } catch (error) {
          // Skip malformed suggestions
          console.warn('BqlAutocompleteService: Error filtering suggestion:', suggestion, error);
          return false;
        }
      });
      
      // Sort results: items starting with prefix come first
      filtered.sort((a, b) => {
        const aValueStarts = a.value.toLowerCase().startsWith(lowerPrefix);
        const aDisplayStarts = a.display.toLowerCase().startsWith(lowerPrefix);
        const bValueStarts = b.value.toLowerCase().startsWith(lowerPrefix);
        const bDisplayStarts = b.display.toLowerCase().startsWith(lowerPrefix);
        
        const aStarts = aValueStarts || aDisplayStarts;
        const bStarts = bValueStarts || bDisplayStarts;
        
        // Items that start with prefix come first
        if (aStarts && !bStarts) return -1;
        if (!aStarts && bStarts) return 1;
        
        // Within each group, sort alphabetically by display
        return a.display.localeCompare(b.display);
      });
      
      return filtered;
    } catch (error) {
      console.error('BqlAutocompleteService: Error filtering suggestions:', error);
      return suggestions || [];
    }
  }

  /**
   * Checks if the cursor is inside a quoted string or regex pattern
   * Returns true if autocomplete should be disabled (user is typing free text)
   */
  private isCursorInQuotedStringOrRegex(query: string, cursorPosition: number): boolean {
    // Check for quoted strings
    let inString = false;
    let stringStart = -1;
    let i = 0;
    
    while (i < cursorPosition) {
      const ch = query[i];
      
      // Handle quoted strings
      if (ch === '"') {
        if (!inString) {
          inString = true;
          stringStart = i;
        } else {
          // Check if it's escaped
          let escapeCount = 0;
          let j = i - 1;
          while (j >= 0 && query[j] === '\\') {
            escapeCount++;
            j--;
          }
          // If odd number of backslashes, the quote is escaped
          if (escapeCount % 2 === 0) {
            inString = false;
            stringStart = -1;
          }
        }
      }
      
      i++;
    }
    
    // If we're inside a string at cursor position, disable autocomplete
    if (inString) {
      return true;
    }
    
    // Check for regex patterns /pattern/ or /pattern/flags
    // Look backwards from cursor to find if we're inside a regex
    let regexStart = -1;
    i = 0;
    let inRegex = false;
    
    while (i < cursorPosition) {
      const ch = query[i];
      
      // Check if this is the start of a regex (/ preceded by space, operator, or start of string)
      if (ch === '/' && !inRegex) {
        const prevChar = i > 0 ? query[i - 1] : ' ';
        // Regex can start after whitespace, operators, or at the beginning
        if (/[\s&|(!,=<>]/.test(prevChar) || i === 0) {
          inRegex = true;
          regexStart = i;
        }
      } else if (ch === '/' && inRegex) {
        // Check if it's escaped
        let escapeCount = 0;
        let j = i - 1;
        while (j >= regexStart && query[j] === '\\') {
          escapeCount++;
          j--;
        }
        // If even number of backslashes (or none), this closes the regex
        if (escapeCount % 2 === 0) {
          // Consume optional flags after closing /
          let k = i + 1;
          while (k < query.length && /[a-z]/i.test(query[k])) {
            k++;
          }
          // If cursor is before the end of flags, we're still in the regex literal
          if (cursorPosition <= k) {
            return true;
          }
          inRegex = false;
          regexStart = -1;
        }
      }
      
      i++;
    }
    
    // If we're inside a regex at cursor position, disable autocomplete
    if (inRegex) {
      return true;
    }
    
    return false;
  }

  /**
   * Analyzes parentheses context to determine if cursor is inside parentheses
   * and find the position of the most recent unmatched opening parenthesis
   */
  private analyzeParenthesesContext(tokens: Token[]): {
    insideParentheses: boolean;
    lastOpenParenIndex: number;
    nestingLevel: number;
  } {
    let nestingLevel = 0;
    let lastOpenParenIndex = -1;

    for (let i = 0; i < tokens.length; i++) {
      const token = tokens[i];
      if (token.type === 'symbol' && token.value === '(') {
        nestingLevel++;
        lastOpenParenIndex = i;
      } else if (token.type === 'symbol' && token.value === ')') {
        nestingLevel--;
        if (nestingLevel === 0) {
          lastOpenParenIndex = -1;
        }
      }
    }

    return {
      insideParentheses: nestingLevel > 0,
      lastOpenParenIndex,
      nestingLevel
    };
  }

  /**
   * Generates a cache key for the QueryBuilderConfig
   * This is used to cache suggestions per configuration
   */
  private generateConfigCacheKey(config: QueryBuilderConfig): string {
    try {
      // Generate a simple hash based on field names and types
      // This is sufficient for detecting config changes
      const fieldKeys = Object.keys(config.fields || {}).sort();
      const fieldSignature = fieldKeys.map(key => {
        const field = config.fields[key];
        return `${key}:${field.type}:${(field.operators || []).join(',')}`;
      }).join('|');
      
      return fieldSignature;
    } catch (error) {
      console.error('BqlAutocompleteService: Error generating cache key', error);
      // Return a timestamp to avoid caching on error
      return Date.now().toString();
    }
  }
  
  /**
   * Limits the number of suggestions to MAX_SUGGESTIONS
   * This improves performance by reducing the number of items rendered in the dropdown
   */
  private limitSuggestions(suggestions: AutocompleteSuggestion[]): AutocompleteSuggestion[] {
    if (suggestions.length <= this.MAX_SUGGESTIONS) {
      return suggestions;
    }
    
    return suggestions.slice(0, this.MAX_SUGGESTIONS);
  }

  /**
   * Analyzes a sequence of tokens to determine the current context
   * This is used for analyzing tokens within parentheses or after logical operators
   */
  private analyzeTokenSequence(
    tokens: Token[],
    config: QueryBuilderConfig,
    cursorPosition: number
  ): AutocompleteContext {
    if (tokens.length === 0) {
      return {
        type: 'field',
        prefix: '',
        cursorPosition
      };
    }

    const lastToken = tokens[tokens.length - 1];

    // Check if last token is a logical operator or opening parenthesis
    if (lastToken.type === 'symbol') {
      if (lastToken.value === '&' || lastToken.value === '|' || lastToken.value === '(') {
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }
      // Handle negation operator - suggest fields after "!"
      if (lastToken.value === '!') {
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }
    }

    // Handle negated operators (!contains, !like, !in, !exists)
    // These are tokenized as operators by the tokenizer
    if (lastToken.type === 'operator') {
      const opLower = lastToken.value.toLowerCase();
      
      // Check if it's a negated operator that requires a value
      if (opLower === '!contains' || opLower === '!like' || opLower === '!in') {
        // These are binary operators - expect value next
        if (tokens.length >= 2) {
          const secondLast = tokens[tokens.length - 2];
          if (secondLast.type === 'word') {
            return {
              type: 'value',
              currentField: secondLast.value,
              currentOperator: lastToken.value,
              prefix: '',
              cursorPosition
            };
          }
        }
      }
      
      // !exists is unary - suggest fields for next condition
      if (opLower === '!exists') {
        return {
          type: 'field',
          prefix: '',
          cursorPosition
        };
      }
    }

    // Single token - could be a partial field name
    if (tokens.length === 1) {
      if (lastToken.type === 'word') {
        return {
          type: 'field',
          prefix: lastToken.value,
          cursorPosition
        };
      }
    }

    // Look for field + operator pattern
    if (tokens.length >= 2) {
      const secondLast = tokens[tokens.length - 2];
      
      // If last token is an operator, we're in value context
      if (lastToken.type === 'operator') {
        // Check if the operator is unary (exists, !exists)
        const opLower = lastToken.value.toLowerCase();
        if (opLower === 'exists' || opLower === '!exists') {
          // After unary operator, suggest fields for next condition
          return {
            type: 'field',
            prefix: '',
            cursorPosition
          };
        }
        
        // Binary operator - expect value next
        if (secondLast.type === 'word') {
          return {
            type: 'value',
            currentField: secondLast.value,
            currentOperator: lastToken.value,
            prefix: '',
            cursorPosition
          };
        }
      }
      
      // If second last is a word (potential field) and last is a word, could be operator
      if (secondLast.type === 'word' && lastToken.type === 'word') {
        // Check if the field exists in config
        if (config.fields[secondLast.value]) {
          // Last token might be a partial operator
          return {
            type: 'operator',
            currentField: secondLast.value,
            prefix: lastToken.value,
            cursorPosition
          };
        }
      }
    }

    // Look for field + operator + value pattern
    if (tokens.length >= 3) {
      const thirdLast = tokens[tokens.length - 3];
      const secondLast = tokens[tokens.length - 2];
      
      // Pattern: field operator value
      if (thirdLast.type === 'word' && secondLast.type === 'operator') {
        if (config.fields[thirdLast.value]) {
          return {
            type: 'value',
            currentField: thirdLast.value,
            currentOperator: secondLast.value,
            prefix: lastToken.value,
            cursorPosition
          };
        }
      }
    }

    // Default: if last token is a word, assume it's a partial field name
    if (lastToken.type === 'word') {
      return {
        type: 'field',
        prefix: lastToken.value,
        cursorPosition
      };
    }

    return {
      type: 'unknown',
      prefix: '',
      cursorPosition
    };
  }
}
