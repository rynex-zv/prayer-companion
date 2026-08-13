##input
Windows Debug automation, route `/settings/tasbih`.

##Actions
Created a preset; edited its name, repeat mode, item text/count; reordered/selected it; then removed it.

##Tested
Twenty workflow assertions plus page-contract checks for every input and accessible DOM name.

##Faild+why
Functional automation: none. UIA remains blocked.

##things to fix
Expose the preset editor controls through UIA.

##remarks
Mutations return the updated affected projection without a required snapshot refresh.

