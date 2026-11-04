"""One checkpoint for original module stages and every other public request shape."""
import argparse
from copy import deepcopy
import hashlib
import importlib.util
import json
from pathlib import Path
import time

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
MODULES={'CharacterSkillModuleSelection','AcquiredTraitModuleSelection',
         'EquipmentEvolutionModuleSelection','FacilityEvolutionModuleSelection'}
FINITE_CHOICES={
    'AcquiredTraitLegacyV2':[('combinationId',)],
    'CharacterSkillLegacyV2':[('candidates','ruleId'),('candidates','combinationId')],
    'EquipmentChoiceLegacyV2':[('selectedIndex',)],
    'FacilityEvolutionLegacyV2':[('proposalIds',),('mutationTags',),('reasons','proposalId')],
    'EvolutionHistoryLegacyV2':[('effectId',)],
    'AcquiredTrait':[('presentationId',)],'CharacterSkill':[('presentationId',)],
    'EvolutionHistory':[('presentationId',)],'FacilityEvolution':[('presentationId',)],
    'MacroGoal':[('targetFacilityId',)],'MoodImpulse':[('targetFacilityId',)],
    'SocialRumor':[('targetFacilityId',)],
}


def validate_request(module,row):
    if not isinstance(row,dict) or not all(k in row for k in ('profile','wire','requestKey','sourceWireSha256')):
        raise ValueError('profile/wire/requestKey/sourceWireSha256 공개 요청이 필요합니다.')
    if row['sourceWireSha256']!='sha256:'+hashlib.sha256(module.canonical(row['wire'])).hexdigest():
        raise ValueError('요청 원문 해시가 일치하지 않습니다.')
    if row['profile'] in MODULES:return module.contract(row)
    contracts=json.loads((HERE/'static-schemas.json').read_bytes())['schemas']
    if row['profile'] not in contracts:raise ValueError('등록되지 않은 요청 종류입니다.')
    wire=row['wire']
    if not isinstance(wire.get('messages'),list) or not wire['messages']:
        raise ValueError('원본 messages가 필요합니다.')
    schema=wire.get('response_format',{}).get('json_schema',{}).get('schema')
    original=contracts[row['profile']]
    if not isinstance(schema,dict) or schema.get('type')!='object' or schema.get('additionalProperties') is not False:
        raise ValueError('제한된 원본 응답 스키마가 필요합니다.')
    if set(schema.get('properties',{}))!=set(original['properties']) or set(schema.get('required',[]))!=set(original['required']):
        raise ValueError('현재 공개 계약의 응답 필드가 다릅니다.')
    def check(narrow,wide):
        if not isinstance(narrow,dict):raise ValueError('잘못된 스키마입니다.')
        from jsonschema import Draft7Validator
        finite=narrow.get('enum',[])
        if finite:
            for value in finite:Draft7Validator(wide).validate(value)
        if 'const' in narrow:
            Draft7Validator(wide).validate(narrow['const'])
            finite=[narrow['const']]
        if narrow.get('type',wide.get('type'))!=wide.get('type'):
            raise ValueError('응답 값의 종류가 다릅니다.')
        if 'enum' in wide and not set(narrow.get('enum',wide['enum']))<=set(wide['enum']):
            raise ValueError('허용되지 않은 선택을 응답 스키마에 추가했습니다.')
        for key in ('maxLength','maxItems','maximum'):
            if key in wide and key not in narrow and not finite:raise ValueError('원래 응답 상한을 없앨 수 없습니다.')
            if key in wide and narrow.get(key,wide[key])>wide[key]:raise ValueError('원래 응답 상한을 늘릴 수 없습니다.')
        for key in ('minLength','minItems','minimum'):
            if key in wide and key not in narrow and not finite:raise ValueError('원래 응답 하한을 없앨 수 없습니다.')
            if key in wide and narrow.get(key,wide[key])<wide[key]:raise ValueError('원래 응답 하한을 낮출 수 없습니다.')
        if 'pattern' in wide and narrow.get('pattern')!=wide['pattern'] and not finite:
            raise ValueError('원래 문자 제약을 없앨 수 없습니다.')
        if wide.get('additionalProperties') is False and narrow.get('additionalProperties') is not False:
            raise ValueError('알 수 없는 응답 필드는 허용하지 않습니다.')
        if wide.get('properties') and set(narrow.get('properties',{}))!=set(wide['properties']):
            raise ValueError('응답 필드 구성이 다릅니다.')
        for key,value in wide.get('properties',{}).items():
            if key not in narrow.get('properties',{}):raise ValueError('응답 필드가 누락됐습니다.')
            check(narrow['properties'][key],value)
        if isinstance(wide.get('items'),dict):check(narrow.get('items',{}),wide['items'])
    check(schema,original)
    for path in FINITE_CHOICES.get(row['profile'],[]):
        node=schema
        for key in path:
            if node.get('type')=='array':node=node['items']
            node=node['properties'][key]
        if node.get('type')=='array' and not ('const' in node or isinstance(node.get('enum'),list) and bool(node['enum'])):
            node=node['items']
        if not ('const' in node or isinstance(node.get('enum'),list) and bool(node['enum'])):
            raise ValueError('허용 후보가 응답 스키마에 고정되지 않았습니다: '+'.'.join(path))
    return None


def generate(module,model,row,folder):
    contract=validate_request(module,row)
    if contract is None:
        schema=row['wire']['response_format']['json_schema']['schema']
        from prioritized_needs_view import messages_for
        return model.call(messages_for(row['profile'],row['wire']['messages']),schema,lambda native:native,folder,'auxiliary',1024)
    from single_decision_view import reading_messages
    from native_alignment_trial import reading_native
    previous=[]
    for index,offer in enumerate(contract['offers']):
        phase=folder/('candidate-'+str(index));phase.mkdir()
        reading=dict(drawbackModuleIds=[])
        if offer['drawback']:
            messages,schema=reading_messages(row,index,previous)
            reading=model.call(messages,schema,reading_native,phase,'reading',512)
        messages,schema=module.selection_messages(row,index,previous,reading)
        binding=model.call(messages,schema,lambda native:module.selection_native(row,index,previous,reading,native),phase,'selection',512)
        errors=module.binding_errors(binding,schema,json.loads(messages[1]['content']))
        if errors:raise ValueError('; '.join(errors))
        decision=deepcopy(binding['decision'])
        if decision['drawbackModuleIds']!=reading['drawbackModuleIds']:
            raise ValueError('실제 모델의 부담 판단이 다음 단계에서 달라졌습니다.')
        messages,schema=module.prose_messages(row,index,decision)
        prose=model.call(messages,schema,lambda native:module.prose_native(row,native),phase,'prose',768)
        previous.append(dict(decision,**prose))
    return module.assemble(row,previous)


def load_model(manifest_path=None):
    spec=importlib.util.spec_from_file_location('single_r78_runtime',ROOT/'Prototype/R68/app.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    manifest=HERE/'model-manifest.json' if manifest_path is None else Path(manifest_path)
    document=json.loads(manifest.read_bytes())
    if document.get('selectionModel') is not None:raise ValueError('이 실행기는 두 번째 모델을 허용하지 않습니다.')
    module.require(document['schema']=='dungeonstory-prototype-model@1' and document['revision']=='R80','현재 추론 버전이 올바르지 않습니다.')
    import re
    module.require(re.fullmatch(r'sha256:[0-9a-f]{64}',document['modelSha256']),'모델 해시가 올바르지 않습니다.')
    model_path=(manifest.parent/document['modelFile']).resolve()
    module.require(model_path.is_relative_to(ROOT.resolve()) and model_path.is_file() and model_path.suffix=='.gguf','현재 모델 작업공간 안의 GGUF 파일만 사용할 수 있습니다.')
    module.MODEL_REVISION,module.MODEL,module.MODEL_SHA,module.OUTPUT=document['revision'],model_path,document['modelSha256'],ROOT/'Artifacts/Prototype/R80-r1'
    module.SELECTION_MODEL=None
    module.PROSE_RECIPE=None
    import scoped_auxiliary_view
    import numeric_free_prose_grammar
    module.prose_messages=scoped_auxiliary_view.prose_messages
    module.prose_native=scoped_auxiliary_view.prose_native
    module.native_constraint=numeric_free_prose_grammar.native_constraint
    class SingleModel(module.Prototype):
        def engine_for_stage(self,stage):
            module.require(stage in ('reading','selection','prose','auxiliary'),'알 수 없는 요청 단계입니다.')
            module.require(self.selection_peer is None,'두 번째 모델은 허용하지 않습니다.')
            return self
        def call(self,messages,schema,transform,folder,stage,limit):
            if stage!='auxiliary':return super().call(messages,schema,transform,folder,stage,limit)
            from jsonschema import Draft7Validator
            Draft7Validator.check_schema(schema)
            prompt=self.tokenizer.apply_chat_template(messages,tokenize=False,add_generation_prompt=True,enable_thinking=False)
            ids=self.tokenizer.encode(prompt,add_special_tokens=False)
            module.require(len(ids)<=5120,'입력이 모델 문맥을 초과했습니다. 자동 자르기는 하지 않습니다.')
            checked,tokenize_raw=module.http_json(self.opener,self.base,'/tokenize',dict(content=prompt,add_special=False,parse_special=True),self.key,30)
            module.require(checked['tokens']==ids,'HF와 실제 엔진의 토큰이 다릅니다.')
            with (folder/'auxiliary-tokenize-http.json').open('xb') as stream:stream.write(tokenize_raw)
            native=transform(deepcopy(schema))
            from behavior_auxiliary_grammar import native_constraint
            decoder,constraint=native_constraint(native)
            module.write_json(folder/'auxiliary-input.json',dict(messages=messages,schema=schema,nativeSchema=native,
                modelSha256=self.model_sha,modelRevision=self.revision,constraint=constraint,outputRepair=False))
            with (folder/'auxiliary-grammar.gbnf').open('x',encoding='utf8',newline='\n') as stream:stream.write(decoder['grammar'])
            body,raw=module.http_json(self.opener,self.base,'/completion',dict(prompt=prompt,**decoder,
                n_predict=limit,temperature=0.,samplers=['temperature'],seed=0,stream=False,cache_prompt=False,
                return_tokens=True,repeat_penalty=1.,presence_penalty=0.,frequency_penalty=0.,ignore_eos=False,
                response_fields=['content','tokens','stop','stop_type','truncated','timings']),self.key,300)
            with (folder/'auxiliary-http.json').open('xb') as stream:stream.write(raw)
            value=json.loads(module.validated_response(body));Draft7Validator(schema).validate(value)
            return value
    return module,SingleModel()


def main():
    p=argparse.ArgumentParser(description='단일 R80 모델의 전체 공개 요청 실행')
    p.add_argument('--request',type=Path,required=True);args=p.parse_args()
    row=json.loads(args.request.read_bytes());module,model=load_model()
    validate_request(module,row)
    try:
        model.boot()
        if model.snapshot()['status']!='ready':raise RuntimeError(model.snapshot().get('error','모델 준비 실패'))
        folder=model.folder/'wire-request';folder.mkdir();module.write_json(folder/'request.json',row)
        started=time.monotonic()
        try:
            value=generate(module,model,row,folder);module.write_json(folder/'result.json',value)
            print(json.dumps(value,ensure_ascii=False,indent=2))
            print('원본 기록: '+str(folder));print('생성 시간: '+str(round(time.monotonic()-started,1))+'초')
        except Exception as error:
            module.write_json(folder/'failure.json',dict(error=str(error),outputRepair=False));raise
    finally:model.close()

if __name__=='__main__':main()
