# CIM integration tools

Contains files specific to the integration with CIM.

## CIM Differ

Example of how to use the CIM.Differ.CLI.

```sh
./CIM.Differ.CLI --previous-state-file='./my-previous-state-file.jsonl' --new-state-file='./my-new-state-file.jsonl' --output-file=''./my-new-outputfile.jsonl''
```

## CIM Mapper

Example of how to use the CIM.Mapper.CLI.

```sh
./CIM.Mapper.CLI --transformation-configuration-file="./TransformationConfig.xml" --new-state-file='specification_one,specification_two,specification_three'
```
