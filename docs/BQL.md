# BQL Language Specification

BQL is a fielded query language used by the query builder, search input, ruleset
representation, and Elasticsearch conversion pipeline. A BQL query is parsed into
a ruleset tree and then translated into an Elasticsearch query.

This document specifies the intended language contract for operators, operands,
literal types, whitespace, and boolean composition.

## Query Structure

A query is one or more rules joined by boolean operators:

```bql
fname = John
fname = John & lname = Quincy
fname = John | fname = Bill
(fname = John | fname = Bill) & dob = 1942
!(fname = John)
```

Operator precedence is:

1. `!`
2. `&`
3. `|`

Parentheses override precedence.

## Whitespace

Whitespace is insignificant between tokens except where it is needed to separate
adjacent word tokens.

Symbolic operators do not require surrounding whitespace:

```bql
fname="John"
numChildren!=3
dob>=1992-09-03
```

Word operators should be separated from adjacent field names and operands by
whitespace unless punctuation separates the tokens:

```bql
fname CONTAINS john
fname IN (john, bill)
fname LIKE ("john%", "bill%")
fname LIKE("john%", "bill%")
```

Commas in lists may have optional surrounding whitespace:

```bql
fname IN (John,"John Quincy",Bill)
fname IN (John, "John Quincy", Bill)
```

Empty lists and trailing commas are invalid:

```bql
fname IN ()
fname IN (John,)
```

## Literals

### Unquoted Text

Unquoted text literals are intended for simple identifier-like values:

```bql
John
abc123
some_value
```

Recommended grammar:

```text
[A-Za-z0-9_]+
```

String values containing spaces or punctuation must be quoted unless they are a
recognized non-string literal such as a date, datetime, number, boolean, or regex.

### Quoted Strings

Quoted strings preserve case, spaces, and punctuation:

```bql
"John Quincy"
"william John"
"John \"Jack\" Smith"
```

### Numbers

Number literals may be integer or decimal and may be negative:

```bql
3
-2
4.5
0.75
```

### Dates

Date values are edited with a segmented mask:

```text
--/--/yyyy
```

Each segment is selected as a unit. Month and day accept either numeric values or
`*`; year accepts numeric values only. Valid date-entry forms are:

```text
*/*/1942     => whole year 1942
09/*/1942    => whole month September 1942
09/03/1942   => that calendar day
```

A day wildcard requires a concrete month or a month wildcard. A concrete day with
a month wildcard is invalid:

```text
*/03/1942    => invalid
```

BQL and ruleset values store dates in normalized form:

```bql
1942
1942-09
1942-09-03
```

Partial dates represent ranges:

```text
1942       => from 1942-01-01 through before 1943-01-01
1942-09    => from 1942-09-01 through before 1942-10-01
1942-09-03 => that calendar day
```

### Datetimes

Datetime values are edited with a segmented mask:

```text
--/--/yyyy --:--
```

Datetime segments accept numeric values only. Datetimes must include a complete
calendar date and a complete 24-hour time through minute precision:

```text
09/06/2026 10:45
```

BQL and ruleset values store datetimes in normalized local datetime form:

```bql
2026-09-06T10:45
```

Seconds, `Z` suffixes, and numeric timezone offsets are not supported by the
query-builder datetime value contract.

### Booleans

Boolean literals are:

```bql
true
false
```

### Regex

Regex literals use slash delimiters:

```bql
/John Q.*/
/john q/i
/\/path\/value/i
```

Supported flags are none or `i`. Other flags are invalid.

Regex operands are valid only for string-like fields and only for operators that
explicitly allow regex.

## Field Type Compatibility

String, category, and document-style fields may use:

```text
unquoted text
quoted strings
numbers as text
regex where the operator permits regex
```

Number fields may use numbers only.

Date fields may use date literals, including partial dates.

Datetime fields may use complete datetime literals. Partial dates are valid for
`date` fields, not `datetime` fields.

Boolean fields may use `true` and `false` only.

## Operators

### `=`

Compares one scalar literal to the whole field value.

Valid:

```bql
fname = John
fname = "John Quincy"
numChildren = 3
dob = 1992-09-03
dob = 1942
logtime = 2026-09-06T10:45
isAlive = true
```

Invalid:

```bql
fname = (John, Bill)
fname = /john/i
```

For string-like fields, equality should be case-insensitive unless the field
specification explicitly requires case-sensitive behavior.

For partial dates, equality means range equality. For example:

```bql
dob = 1942
```

means `dob >= 1942-01-01` and `dob < 1943-01-01`.

### `!=`

Negated whole-field equality.

Valid:

```bql
fname != John
numChildren != 3
dob != 1942
isAlive != false
```

The operand restrictions are the same as `=`.

### `>`, `>=`, `<`, `<=`

Ordered comparison against one scalar literal.

Valid for number, date, and datetime fields:

```bql
numChildren > 2
dob >= 1992-09-03
dob < 1942
logtime > 2026-09-06T10:45
```

Invalid:

```bql
fname > John
dob > /1992/
numChildren > (1, 2, 3)
```

Partial dates compare by their represented range. For example:

```bql
dob > 1942
```

means dates after the 1942 range, effectively `dob >= 1943-01-01`.

```bql
dob <= 1942
```

means dates through the 1942 range, effectively `dob < 1943-01-01`.

### `IN`

Whole-field equality against any value in a non-empty parenthesized list.

Valid:

```bql
fname IN (John, "John Quincy", Bill)
numChildren IN (3, 4, 5)
dob IN (1942, 1943, 1944)
dob IN (1992-09-03, 2001-04-12)
logtime IN (2026-09-06T10:45, 2026-09-07T11:00)
```

For string-like fields, regex list members may be allowed:

```bql
fname IN (John, "John Quincy", /John Q.*/i)
```

Semantics: match if the field equals any literal value or satisfies any regex
member.

For number, date, datetime, and boolean fields, regex values are invalid.

### `!IN`

Negated `IN`.

```bql
fname !IN (John, Bill)
numChildren !IN (3, 4, 5)
dob !IN (1942, 1943)
```

Semantics: the field does not match any listed operand.

### `CONTAINS`

String-oriented partial match.

Valid:

```bql
fname CONTAINS bill
fname CONTAINS "william John"
fname CONTAINS /john q/i
document CONTAINS "civil rights"
```

`CONTAINS` is intended for string-like fields. It should not be used for numeric,
date, datetime, or boolean comparison.

### `CONTAINS (...)`

Partial match against any value in a non-empty list.

Valid:

```bql
fname CONTAINS (john, /john q/i, william)
document CONTAINS ("civil rights", /equal protection/i)
```

Semantics: OR across operands.

### `!CONTAINS`

Negated partial match.

```bql
fname !CONTAINS bill
fname !CONTAINS (john, william)
```

### `LIKE`

SQL-like string pattern match or regex match.

Valid:

```bql
fname LIKE "John Q%"
fname LIKE "J_hn"
fname LIKE /john q/i
```

Wildcard rules:

```text
%  => zero or more characters
_  => exactly one character
\% => literal percent
\_ => literal underscore
\\ => literal backslash
```

`LIKE` is intended for string-like fields only.

### `LIKE (...)`

Pattern match against any pattern in a non-empty list.

Valid:

```bql
fname LIKE ("john q", "will%", /john q/i)
```

Semantics: OR across operands.

### `!LIKE`

Negated `LIKE`.

```bql
fname !LIKE "John Q%"
fname !LIKE ("john%", /bill/i)
```

### `EXISTS`

Unary field-existence check. It does not take an operand.

```bql
fname EXISTS
dob EXISTS
```

### `!EXISTS`

Negated field-existence check. It does not take an operand.

```bql
fname !EXISTS
dob !EXISTS
```

## List Rules

List-capable operators are:

```text
IN
!IN
CONTAINS
!CONTAINS
LIKE
!LIKE
```

Lists must:

1. Be enclosed in parentheses.
2. Contain at least one value.
3. Use commas between values.
4. Not contain a trailing comma.

Valid:

```bql
fname IN (John, Bill)
fname CONTAINS ("civil rights", /equal protection/i)
fname LIKE ("john%", "bill%")
```

Invalid:

```bql
fname IN ()
fname IN (John,)
fname IN (John Bill)
```

## Ruleset Representation

Single-value rules should be represented with a scalar `value`:

```json
{
  "field": "fname",
  "operator": "=",
  "value": "John Quincy"
}
```

List-capable rules should be represented with an array `value`:

```json
{
  "field": "fname",
  "operator": "in",
  "value": ["John", "John Quincy"]
}
```

Date and datetime rules should store normalized string values, not the segmented
entry mask:

```json
{ "field": "dob", "operator": "=", "value": "1942" }
{ "field": "dob", "operator": "=", "value": "1942-09" }
{ "field": "dob", "operator": "=", "value": "1942-09-03" }
{ "field": "logtime", "operator": "=", "value": "2026-09-06T10:45" }
```

Regex values should ideally use a typed representation to avoid ambiguity between
a literal string that looks like a regex and an actual regex operand:

```json
{
  "kind": "regex",
  "pattern": "John Q.*",
  "flags": "i"
}
```

Example with a regex in a list:

```json
{
  "field": "fname",
  "operator": "in",
  "value": [
    "John",
    "John Quincy",
    {
      "kind": "regex",
      "pattern": "John Q.*",
      "flags": "i"
    }
  ]
}
```

## Validation Summary

`=`, `!=`, `>`, `>=`, `<`, and `<=` accept exactly one non-regex scalar value.

`IN`, `!IN`, `CONTAINS`, `!CONTAINS`, `LIKE`, and `!LIKE` accept non-empty
parenthesized lists where specified.

`CONTAINS` and `LIKE` are string-style operators.

Dates are entered as `--/--/yyyy` segments and stored as `YYYY`, `YYYY-MM`, or
`YYYY-MM-DD` literals. Month/day wildcards are entered with `*`.

Datetimes are entered as `--/--/yyyy --:--` segments and stored as
`YYYY-MM-DDTHH:mm` literals. Timezone suffixes and seconds are not supported.

Regex literals support no flags or the `i` flag only.
