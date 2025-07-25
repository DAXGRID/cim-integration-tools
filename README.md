# CIM integration tools

Contains files specific to the integration with CIM.

## CIM Differ

Example of how to use the CIM.Differ.CLI.

```sh
./CIM.Differ.CLI --previous-state-file='./my-previous-state-file.jsonl' --new-state-file='./my-new-state-file.jsonl' --output-file='./my-new-outputfile.jsonl'
```

## CIM Mapper

Example of how to use the CIM.Mapper.CLI.

```sh
./CIM.Mapper.CLI --transformation-configuration-file="./TransformationConfig.xml" --transformation-specification-name='specification_one,specification_two,specification_three'
```

## CIM Postgres Importer

Example of how to use the CIM.PostgresImporter.CLI.

```sh
./CIM.PostgresImporter.CLI \
  --input-file-path="./my-input-file.jsonl" \
  --srid=25812 \
  --connection-string="Server=localhost;Port=5432;Database=postgres;User Id=postgres;Password=postgres;"
```

## CIM Topology Processor

```sh
./CIM.TopologyProcessor.CLI \
  --input-file="./my-input-file.jsonl" \
  --output-file="./my-output-file.jsonl"
```

## CIM Filter

```sh
./CIM.Filter.CLI \
    --input-file-path=./mapper_output.jsonl \
    --output-file-path=./filter_output.json \
    --base-voltage-lower-bound=400 \
    --base-voltage-upper-bound=9999
```

## CIM Validator

```sh
./CIM.Validator.CLI \
    --input-file=./mapper_output.jsonl \
    --output-file=./filter_output.json
```

## CIM Pre-validator

```sh
./CIM.PreValidator.CLI \
    --input-file=./mapper_output.jsonl \
    --output-file=./filter_output.json
```
